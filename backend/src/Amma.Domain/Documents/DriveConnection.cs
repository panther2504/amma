using Amma.Domain.Common;

namespace Amma.Domain.Documents;

/// <summary>
/// The family's own Google Drive, used to keep full-size memory photos. Secrets are stored encrypted
/// (Storage:EncryptionKey). Amma only has the "drive.file" permission: it can see the files it created, nothing else.
/// </summary>
public class DriveConnection : Entity, IUserOwned
{
    public Guid UserId { get; set; }
    public string ClientId { get; set; } = "";
    public byte[] ClientSecret { get; set; } = [];
    public byte[]? RefreshToken { get; set; }
    public string? FolderId { get; set; }
    public string? Email { get; set; }
    public DateTimeOffset? ConnectedAt { get; set; }
}
