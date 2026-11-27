using Amma.Domain.Common;

namespace Amma.Domain.Assistant;

public class AssistantMessage : Entity, IUserOwned
{
    public Guid UserId { get; set; }
    /// <summary>"user" or "assistant".</summary>
    public string Role { get; set; } = "user";
    public string Content { get; set; } = "";
    public bool IsEmergency { get; set; }
    public string? Topic { get; set; }
}
