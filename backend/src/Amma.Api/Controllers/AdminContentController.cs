using Amma.Api.Auth;
using Amma.Api.Contracts;
using Amma.Api.Services;
using Amma.Domain.Common;
using Amma.Domain.Content;
using Amma.Domain.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Amma.Api.Controllers;

/// <summary>
/// Medical content workflow: Draft → InReview → Approved (medical reviewer only) → Published → Retired.
/// Any edit sends content back for review and bumps its version.
/// </summary>
[Route("api/v1/admin/content")]
[Authorize(Roles = Roles.ContentEditors)]
public class AdminContentController(AuditService audit) : ApiControllerBase
{
    private static readonly Dictionary<string, Type> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        ["foods"] = typeof(FoodItem), ["articles"] = typeof(Article), ["weeks"] = typeof(PregnancyWeek),
        ["guides"] = typeof(WeeklyGuide), ["meals"] = typeof(MealPlanItem), ["snippets"] = typeof(ContentSnippet),
    };

    [HttpGet("{type}")]
    public async Task<ActionResult<IEnumerable<AdminContentItem>>> List(string type, [FromQuery] ContentStatus? status, CancellationToken ct)
    {
        if (!Types.TryGetValue(type, out var t)) return NotFound();
        var items = await Query(t).ToListAsync(ct);
        return items.Where(i => status is null || i.Status == status).Select(i => new AdminContentItem(i.Id, type, TitleOf(i), i.Status.ToString(), i.Version, i.UpdatedAt, i.ReviewedAt, i.ReviewNotes)).ToList();
    }

    [HttpPost("foods")]
    [Authorize(Roles = Roles.Admin + "," + Roles.ContentManager)]
    public async Task<ActionResult<FoodDto>> CreateFood(SaveFoodRequest req, CancellationToken ct)
    {
        var f = new FoodItem();
        ApplyFood(f, req);
        Db.FoodItems.Add(f);
        await Db.SaveChangesAsync(ct);
        return f.ToDto();
    }

    [HttpPut("foods/{id:guid}")]
    [Authorize(Roles = Roles.Admin + "," + Roles.ContentManager)]
    public async Task<ActionResult<FoodDto>> UpdateFood(Guid id, SaveFoodRequest req, CancellationToken ct)
    {
        var f = await Db.FoodItems.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (f is null) return NotFound();
        ApplyFood(f, req);
        Edited(f);
        await Db.SaveChangesAsync(ct);
        return f.ToDto();
    }

    [HttpPost("articles")]
    [Authorize(Roles = Roles.Admin + "," + Roles.ContentManager)]
    public async Task<ActionResult<ArticleDto>> CreateArticle(SaveArticleRequest req, CancellationToken ct)
    {
        if (await Db.Articles.AnyAsync(a => a.Slug == req.Slug, ct)) return Problem422("An article with that slug already exists.");
        var a = new Article();
        ApplyArticle(a, req);
        Db.Articles.Add(a);
        await Db.SaveChangesAsync(ct);
        return a.ToDto();
    }

    [HttpPut("articles/{id:guid}")]
    [Authorize(Roles = Roles.Admin + "," + Roles.ContentManager)]
    public async Task<ActionResult<ArticleDto>> UpdateArticle(Guid id, SaveArticleRequest req, CancellationToken ct)
    {
        var a = await Db.Articles.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return NotFound();
        ApplyArticle(a, req);
        Edited(a);
        await Db.SaveChangesAsync(ct);
        return a.ToDto();
    }

    [HttpPost("{type}/{id:guid}/{step:regex(^(submit|approve|reject|publish|retire)$)}")]
    public async Task<IActionResult> Transition(string type, Guid id, string step, ContentReviewRequest? req, CancellationToken ct)
    {
        if (!Types.TryGetValue(type, out var t)) return NotFound();
        if (await Db.FindAsync(t, id) is not ReviewableContent item) return NotFound();
        var me = await MeAsync(ct);

        switch (step)
        {
            case "submit" when item.Status is ContentStatus.Draft:
                item.Status = ContentStatus.InReview; break;
            case "approve" or "reject" when item.Status is ContentStatus.InReview:
                // Only qualified medical reviewers may approve medical guidance.
                if (me.Role != UserRole.MedicalReviewer) return Forbid();
                item.Status = step == "approve" ? ContentStatus.Approved : ContentStatus.Draft;
                item.ReviewedByUserId = me.Id; item.ReviewedAt = Clock.UtcNow; item.ReviewNotes = req?.Notes;
                item.NextReviewDue = req?.NextReviewDue ?? DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1));
                break;
            case "publish" when item.Status is ContentStatus.Approved:
                if (me.Role is not (UserRole.Admin or UserRole.ContentManager)) return Forbid();
                item.Status = ContentStatus.Published; break;
            case "retire" when item.Status is not ContentStatus.Retired:
                if (me.Role is not (UserRole.Admin or UserRole.ContentManager)) return Forbid();
                item.Status = ContentStatus.Retired; break;
            default:
                return Problem422($"Cannot {step} content that is {item.Status}.");
        }
        audit.Record(me.Id, $"content.{step}", t.Name, id);
        await Db.SaveChangesAsync(ct);
        return NoContent();
    }

    private IQueryable<ReviewableContent> Query(Type t) => t.Name switch
    {
        nameof(FoodItem) => Db.FoodItems.AsNoTracking(),
        nameof(Article) => Db.Articles.AsNoTracking(),
        nameof(PregnancyWeek) => Db.PregnancyWeeks.AsNoTracking(),
        nameof(WeeklyGuide) => Db.WeeklyGuides.AsNoTracking(),
        nameof(MealPlanItem) => Db.MealPlanItems.AsNoTracking(),
        _ => Db.ContentSnippets.AsNoTracking(),
    };

    private static string TitleOf(ReviewableContent c) => c switch
    {
        FoodItem f => f.Name, Article a => a.Title, PregnancyWeek w => $"Week {w.Week} · {w.SizeName}",
        WeeklyGuide g => $"Weeks {g.FromWeek}–{g.ToWeek}", MealPlanItem m => $"{m.Diet} · {m.Slot}: {m.Description}",
        ContentSnippet s => $"{s.Kind}: {s.Title}", _ => c.Id.ToString(),
    };

    private static void Edited(ReviewableContent c) { c.Version++; c.Status = ContentStatus.InReview; c.ReviewedAt = null; c.ReviewedByUserId = null; }

    private static void ApplyFood(FoodItem f, SaveFoodRequest r)
    {
        f.Name = r.Name; f.NameTa = r.NameTa; f.Emoji = r.Emoji; f.Category = r.Category; f.Safety = r.Safety; f.Nutrients = r.Nutrients;
        f.Benefits = r.Benefits; f.Preparation = r.Preparation; f.Precautions = r.Precautions; f.TrimesterNotes = r.TrimesterNotes;
        f.References = r.References ?? []; f.Tags = r.Tags ?? [];
    }

    private static void ApplyArticle(Article a, SaveArticleRequest r)
    {
        a.Slug = r.Slug; a.Category = r.Category; a.Emoji = r.Emoji; a.ColorKey = r.ColorKey ?? "blush"; a.ReadMinutes = r.ReadMinutes;
        a.Title = r.Title; a.Paragraphs = r.Paragraphs; a.FromWeek = r.FromWeek; a.ToWeek = r.ToWeek;
    }
}
