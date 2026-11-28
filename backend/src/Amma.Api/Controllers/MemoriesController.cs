using Amma.Api.Contracts;
using Amma.Domain.Documents;
using Amma.Domain.Pregnancy;
using Amma.Infrastructure.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Amma.Api.Controllers;

/// <summary>Photo memories. The app resizes photos before upload; a small thumbnail is kept inline for offline viewing.</summary>
[Route("api/v1/memories")]
public class MemoriesController(IFileStorage storage) : ApiControllerBase
{
    private const long MaxBytes = 8 * 1024 * 1024;

    [HttpGet]
    public async Task<IEnumerable<MemoryDto>> List(CancellationToken ct) =>
        (await Db.MemoryPhotos.AsNoTracking().Where(m => m.UserId == UserId).OrderByDescending(m => m.TakenOn).ThenByDescending(m => m.CreatedAt).ToListAsync(ct))
        .Select(ToDto);

    [HttpPost]
    [RequestSizeLimit(MaxBytes + 512 * 1024)]
    public async Task<ActionResult<MemoryDto>> Add([FromForm] IFormFile photo, [FromForm] string thumbnail, [FromForm] string? caption, [FromForm] DateOnly? takenOn, CancellationToken ct)
    {
        if (photo.Length is 0 or > MaxBytes) return Problem422("Photos must be smaller than 8 MB.");
        if (photo.ContentType is not ("image/jpeg" or "image/png" or "image/webp")) return Problem422("Please choose a photo.");
        if (!thumbnail.StartsWith("data:image/", StringComparison.Ordinal) || thumbnail.Length > 200_000) return Problem422("Invalid thumbnail.");

        var today = await TodayAsync(ct);
        var date = takenOn ?? today;
        var p = await ActivePregnancyAsync(ct);
        int? week = p is null ? null : PregnancyCalculator.Calculate(p.Lmp, date).Weeks;
        await using var s = photo.OpenReadStream();
        var m = new MemoryPhoto
        {
            UserId = UserId, TakenOn = date, Caption = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim()[..Math.Min(caption.Trim().Length, 300)],
            PregnancyWeek = week is > 0 and <= 42 ? week : null, PhotoKey = await storage.SaveAsync(s, ct), ThumbnailDataUrl = thumbnail,
            SizeBytes = photo.Length, ByName = await PersonNameAsync(ct),
        };
        Db.MemoryPhotos.Add(m);
        await Db.SaveChangesAsync(ct);
        return ToDto(m);
    }

    [HttpGet("{id:guid}/photo")]
    public async Task<IActionResult> Photo(Guid id, CancellationToken ct)
    {
        var m = await Db.MemoryPhotos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.UserId == UserId, ct);
        if (m is null) return NotFound();
        return File(await storage.OpenAsync(m.PhotoKey, ct), "image/jpeg", $"amma-{m.TakenOn:yyyy-MM-dd}.jpg");
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<MemoryDto>> Edit(Guid id, [FromBody] EditMemoryRequest req, CancellationToken ct)
    {
        var m = await Db.MemoryPhotos.FirstOrDefaultAsync(x => x.Id == id && x.UserId == UserId, ct);
        if (m is null) return NotFound();
        m.Caption = string.IsNullOrWhiteSpace(req.Caption) ? null : req.Caption.Trim();
        await Db.SaveChangesAsync(ct);
        return ToDto(m);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var m = await Db.MemoryPhotos.FirstOrDefaultAsync(x => x.Id == id && x.UserId == UserId, ct);
        if (m is null) return NotFound();
        await storage.DeleteAsync(m.PhotoKey, ct);
        Db.MemoryPhotos.Remove(m);
        await Db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static MemoryDto ToDto(MemoryPhoto m) => new(m.Id, m.TakenOn, m.Caption, m.PregnancyWeek, m.ThumbnailDataUrl, m.SizeBytes, m.ByName, m.CreatedAt);
}

public record EditMemoryRequest(string? Caption);
