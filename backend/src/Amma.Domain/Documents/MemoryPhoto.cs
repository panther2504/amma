using Amma.Domain.Common;

namespace Amma.Domain.Documents;

/// <summary>A photo kept as a memory of the pregnancy (and after).</summary>
public class MemoryPhoto : Entity, IUserOwned
{
    public Guid UserId { get; set; }
    public DateOnly TakenOn { get; set; }
    public string? Caption { get; set; }
    public int? PregnancyWeek { get; set; }
    public string PhotoKey { get; set; } = "";
    /// <summary>Small JPEG kept inline so the gallery works offline.</summary>
    public string ThumbnailDataUrl { get; set; } = "";
    public long SizeBytes { get; set; }
    public string? ByName { get; set; }
}
