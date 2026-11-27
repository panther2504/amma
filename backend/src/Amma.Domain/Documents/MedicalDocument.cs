using Amma.Domain.Common;

namespace Amma.Domain.Documents;

public enum DocumentCategory { Ultrasound = 0, Blood = 1, Urine = 2, Prescription = 3, Vaccination = 4, Discharge = 5, Other = 99 }

public class MedicalDocument : Entity, IUserOwned
{
    public Guid UserId { get; set; }
    public string Title { get; set; } = "";
    public DocumentCategory Category { get; set; }
    public DateOnly DocumentDate { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "application/octet-stream";
    public long SizeBytes { get; set; }
    /// <summary>Opaque key in the configured file store. Never exposed to clients.</summary>
    public string StorageKey { get; set; } = "";
    public string? Notes { get; set; }
}

public class DocumentAccessLog
{
    public long Id { get; set; }
    public Guid DocumentId { get; set; }
    public Guid AccessedByUserId { get; set; }
    public string Action { get; set; } = "download";
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}
