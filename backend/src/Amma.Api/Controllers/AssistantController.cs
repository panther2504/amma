using Amma.Api.Contracts;
using Amma.Api.Services;
using Amma.Domain.Assistant;
using Amma.Domain.Pregnancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Amma.Api.Controllers;

/// <summary>"Ask Amma" — educational answers only. Never diagnoses or prescribes; emergencies are always escalated first.</summary>
[Route("api/v1/assistant")]
public class AssistantController(IAssistantResponder responder) : ApiControllerBase
{
    private static readonly string[] Suggestions =
        ["Can I eat papaya?", "Is drumstick leaves good?", "Foods with iron", "What to eat this week?", "Why do I feel nauseous?", "Questions for my doctor", "What does AFI mean?"];

    [HttpGet("messages")]
    public async Task<IEnumerable<AssistantMessageDto>> History([FromQuery] int take = 50, CancellationToken ct = default) =>
        (await Db.AssistantMessages.AsNoTracking().Where(m => m.UserId == UserId).OrderByDescending(m => m.CreatedAt).Take(Math.Clamp(take, 1, 200)).ToListAsync(ct))
        .OrderBy(m => m.CreatedAt).Select(ToDto);

    [HttpPost("messages")]
    public async Task<AskResponse> Ask(AskRequest req, CancellationToken ct)
    {
        var me = await MeAsync(ct);
        var p = await ActivePregnancyAsync(ct);
        var week = p is null ? 0 : PregnancyCalculator.Calculate(p.Lmp, Clock.Today(me.TimeZone)).Weeks;
        var ctx = new AssistantContext(me.Name, week, p?.Diet ?? DietPreference.Vegetarian, p?.FoodAllergies ?? [], me.Language);
        var reply = await responder.ReplyAsync(req.Text.Trim(), ctx, ct);

        var q = new AssistantMessage { UserId = UserId, Role = "user", Content = req.Text.Trim(), IsEmergency = reply.IsEmergency };
        var a = new AssistantMessage { UserId = UserId, Role = "assistant", Content = reply.Html, IsEmergency = reply.IsEmergency, Topic = reply.Topic, CreatedAt = q.CreatedAt.AddMilliseconds(1) };
        Db.AssistantMessages.AddRange(q, a);
        await Db.SaveChangesAsync(ct);
        return new AskResponse(ToDto(q), ToDto(a), Suggestions);
    }

    /// <summary>Deletes the whole conversation history (privacy).</summary>
    [HttpDelete("messages")]
    public async Task<IActionResult> Clear(CancellationToken ct)
    {
        Db.AssistantMessages.RemoveRange(await Db.AssistantMessages.Where(m => m.UserId == UserId).ToListAsync(ct));
        await Db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static AssistantMessageDto ToDto(AssistantMessage m) => new(m.Id, m.Role, m.Content, m.IsEmergency, m.CreatedAt);
}
