using Amma.Api.Contracts;
using Amma.Api.Services;
using Amma.Domain.Common;
using Amma.Domain.Content;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Amma.Api.Controllers;

/// <summary>Educational content. Public (no login) so that safety information is always available and can be cached offline.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1/content")]
public class ContentController(ContentQuery content, TimeProvider clock) : ControllerBase
{
    /// <summary>Everything the app needs in one cacheable response (used for offline access).</summary>
    [HttpGet("bundle")]
    [ResponseCache(Duration = 300)]
    public async Task<ContentBundle> Bundle([FromQuery] string lang = "en", CancellationToken ct = default)
    {
        lang = lang is "ta" ? "ta" : "en";
        // Fall back to English for any content that has not been translated yet.
        async Task<List<T>> Get<T>() where T : ReviewableContent
        {
            var rows = await content.Visible<T>(lang).ToListAsync(ct);
            return rows.Count > 0 || lang == "en" ? rows : await content.Visible<T>("en").ToListAsync(ct);
        }
        return new ContentBundle(lang, clock.GetUtcNow(),
            (await Get<PregnancyWeek>()).OrderBy(w => w.Week).Select(w => w.ToDto()).ToList(),
            (await Get<WeeklyGuide>()).OrderBy(g => g.FromWeek).Select(g => g.ToDto()).ToList(),
            (await Get<FoodItem>()).OrderBy(f => f.Safety).ThenBy(f => f.Name).Select(f => f.ToDto()).ToList(),
            (await Get<MealPlanItem>()).OrderBy(m => m.Diet).ThenBy(m => m.Order).Select(m => m.ToDto()).ToList(),
            (await Get<Article>()).OrderBy(a => a.CreatedAt).Select(a => a.ToDto()).ToList(),
            (await Get<ContentSnippet>()).OrderBy(s => s.Kind).ThenBy(s => s.Order).Select(s => s.ToDto()).ToList());
    }

    [HttpGet("foods")]
    public async Task<IEnumerable<FoodDto>> Foods([FromQuery] string? search, [FromQuery] FoodSafety? safety, [FromQuery] string? category, CancellationToken ct)
    {
        var q = content.Visible<FoodItem>();
        if (safety is not null) q = q.Where(f => f.Safety == safety);
        if (!string.IsNullOrWhiteSpace(category)) q = q.Where(f => f.Category == category);
        var list = await q.OrderBy(f => f.Name).ToListAsync(ct);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            list = list.Where(f => f.Name.ToLowerInvariant().Contains(s) || (f.NameTa ?? "").Contains(s) || f.Category.ToLowerInvariant().Contains(s) || f.Tags.Any(t => t.Contains(s))).ToList();
        }
        return list.Select(f => f.ToDto());
    }

    [HttpGet("weeks/{week:int}")]
    public async Task<ActionResult<object>> Week(int week, CancellationToken ct)
    {
        var w = await content.WeekAsync(week, ct);
        var g = await content.GuideAsync(week, ct);
        if (w is null) return NotFound();
        return new { week = w.ToDto(), guide = g?.ToDto() };
    }

    [HttpGet("articles/{slug}")]
    public async Task<ActionResult<ArticleDto>> Article(string slug, CancellationToken ct)
    {
        var a = await content.Visible<Article>().FirstOrDefaultAsync(x => x.Slug == slug, ct);
        return a is null ? NotFound() : a.ToDto();
    }
}
