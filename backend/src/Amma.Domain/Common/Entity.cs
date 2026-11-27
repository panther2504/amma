namespace Amma.Domain.Common;

/// <summary>Base type for all persisted entities.</summary>
public abstract class Entity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Entities that belong to a single user (mother account). Always filtered by owner.</summary>
public interface IUserOwned
{
    Guid UserId { get; set; }
}

/// <summary>Medical content must pass review before it is shown to users.</summary>
public enum ContentStatus
{
    Draft = 0,
    InReview = 1,
    Approved = 2,
    Published = 3,
    Retired = 4,
}

/// <summary>Base for medically reviewed content (foods, articles, weekly guides...).</summary>
public abstract class ReviewableContent : Entity
{
    public ContentStatus Status { get; set; } = ContentStatus.Draft;
    public int Version { get; set; } = 1;
    public string Language { get; set; } = "en";
    public Guid? ReviewedByUserId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public DateOnly? NextReviewDue { get; set; }
    public string? ReviewNotes { get; set; }
}
