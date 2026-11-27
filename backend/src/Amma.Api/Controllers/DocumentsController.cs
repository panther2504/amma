using Amma.Api.Contracts;
using Amma.Api.Services;
using Amma.Domain.Documents;
using Amma.Infrastructure.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Amma.Api.Controllers;

/// <summary>Secure medical report vault. Files are stored outside the database, optionally encrypted at rest.</summary>
[Route("api/v1/documents")]
public class DocumentsController(IFileStorage storage, AuditService audit) : ApiControllerBase
{
    private const long MaxBytes = 15 * 1024 * 1024;
    private static readonly string[] Allowed = ["application/pdf", "image/jpeg", "image/png", "image/webp", "image/heic"];

    [HttpGet]
    public async Task<IEnumerable<DocumentDto>> List([FromQuery] DocumentCategory? category, [FromQuery] string? search, CancellationToken ct)
    {
        var q = Db.MedicalDocuments.AsNoTracking().Where(d => d.UserId == UserId);
        if (category is not null) q = q.Where(d => d.Category == category);
        var list = await q.OrderByDescending(d => d.DocumentDate).ThenByDescending(d => d.CreatedAt).ToListAsync(ct);
        if (!string.IsNullOrWhiteSpace(search))
            list = list.Where(d => d.Title.Contains(search, StringComparison.OrdinalIgnoreCase) || (d.Notes ?? "").Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
        return list.Select(d => d.ToDto());
    }

    [HttpPost]
    [RequestSizeLimit(MaxBytes + 64 * 1024)]
    public async Task<ActionResult<DocumentDto>> Upload([FromForm] IFormFile file, [FromForm] string title, [FromForm] DocumentCategory category,
        [FromForm] DateOnly? documentDate, [FromForm] string? notes, CancellationToken ct)
    {
        if (file.Length is 0 or > MaxBytes) return Problem422("Files must be between 1 byte and 15 MB.");
        if (!Allowed.Contains(file.ContentType)) return Problem422("Please upload a PDF or an image (JPG, PNG, WEBP, HEIC).");
        if (string.IsNullOrWhiteSpace(title) || title.Length > 160) return Problem422("Please give the document a title.");

        await using var stream = file.OpenReadStream();
        var key = await storage.SaveAsync(stream, ct);
        var doc = new MedicalDocument
        {
            UserId = UserId, Title = title.Trim(), Category = category, DocumentDate = documentDate ?? await TodayAsync(ct),
            FileName = Path.GetFileName(file.FileName), ContentType = file.ContentType, SizeBytes = file.Length, StorageKey = key, Notes = notes?.Trim(),
        };
        Db.MedicalDocuments.Add(doc);
        audit.Record(UserId, "document.upload", "MedicalDocument", doc.Id);
        await Db.SaveChangesAsync(ct);
        return doc.ToDto();
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var doc = await Db.MedicalDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id && d.UserId == UserId, ct);
        if (doc is null) return NotFound();
        Db.DocumentAccessLogs.Add(new DocumentAccessLog { DocumentId = id, AccessedByUserId = UserId });
        await Db.SaveChangesAsync(ct);
        return File(await storage.OpenAsync(doc.StorageKey, ct), doc.ContentType, doc.FileName);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var doc = await Db.MedicalDocuments.FirstOrDefaultAsync(d => d.Id == id && d.UserId == UserId, ct);
        if (doc is null) return NotFound();
        await storage.DeleteAsync(doc.StorageKey, ct);
        Db.MedicalDocuments.Remove(doc);
        audit.Record(UserId, "document.delete", "MedicalDocument", id);
        await Db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// Plain-language help for a report. Phase 1 returns a curated glossary for the report category;
    /// AI summarisation of the actual document is planned for Phase 3. Never a diagnosis.
    /// </summary>
    [HttpGet("{id:guid}/explain")]
    public async Task<ActionResult<DocumentExplanation>> Explain(Guid id, CancellationToken ct)
    {
        var doc = await Db.MedicalDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id && d.UserId == UserId, ct);
        if (doc is null) return NotFound();
        var terms = doc.Category switch
        {
            DocumentCategory.Ultrasound => new Dictionary<string, string> { ["CRL"] = "Crown-rump length — baby's length in early pregnancy, used for dating.", ["BPD"] = "Biparietal diameter — the width of baby's head.", ["EFW"] = "Estimated fetal weight.", ["AFI"] = "Amniotic fluid index — the amount of fluid around baby.", ["FHR"] = "Fetal heart rate.", ["Placenta"] = "Where the placenta is attached (e.g. anterior, posterior)." },
            DocumentCategory.Blood => new Dictionary<string, string> { ["Hb"] = "Haemoglobin — carries oxygen in your blood. Low values can mean anaemia.", ["Platelets"] = "Cells that help blood clot.", ["Blood group / Rh"] = "Your blood type, e.g. B positive.", ["TSH"] = "Thyroid-stimulating hormone — checks thyroid function.", ["OGTT"] = "Oral glucose tolerance test — screens for gestational diabetes." },
            DocumentCategory.Urine => new Dictionary<string, string> { ["Protein / albumin"] = "Protein in urine — your doctor checks this along with blood pressure.", ["Sugar"] = "Glucose in urine.", ["Pus cells"] = "Can suggest an infection — your doctor will interpret this." },
            DocumentCategory.Prescription => new Dictionary<string, string> { ["OD"] = "Once a day.", ["BD"] = "Twice a day.", ["TDS"] = "Three times a day.", ["HS"] = "At bedtime.", ["AC / PC"] = "Before food / after food." },
            DocumentCategory.Vaccination => new Dictionary<string, string> { ["Td"] = "Tetanus-diphtheria vaccine, commonly given in pregnancy in India.", ["Tdap"] = "Tetanus, diphtheria and pertussis vaccine." },
            _ => new Dictionary<string, string>(),
        };
        return new DocumentExplanation(doc.Title,
            "This report records measurements from your visit. Below are common terms you may see on this type of report, explained in simple words.",
            terms.ToList(),
            ["What does each highlighted value mean for me?", "Is anything outside the expected range?", "Do I need any follow-up test?"],
            "Amma does not diagnose or replace your doctor's interpretation of this report.");
    }
}
