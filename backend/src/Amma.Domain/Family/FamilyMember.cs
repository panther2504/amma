using Amma.Domain.Common;

namespace Amma.Domain.Family;

public enum FamilyRole { Partner = 1, FamilyViewer = 2 }
public enum FamilyMemberStatus { Invited = 0, Active = 1, Revoked = 2 }

/// <summary>
/// A person the mother has chosen to share parts of her journey with.
/// The mother (owner) controls every permission; health records and reports are private by default.
/// </summary>
public class FamilyMember : Entity, IUserOwned
{
    /// <summary>The mother who owns the sharing relationship.</summary>
    public Guid UserId { get; set; }
    public Guid? MemberUserId { get; set; }
    public string Name { get; set; } = "";
    public string Relation { get; set; } = "";
    public string? Phone { get; set; }
    public FamilyRole Role { get; set; } = FamilyRole.FamilyViewer;
    public FamilyMemberStatus Status { get; set; } = FamilyMemberStatus.Invited;
    public string InviteCode { get; set; } = "";

    public bool ShareAppointments { get; set; } = true;
    public bool ShareMedicines { get; set; }
    public bool ShareBabyUpdates { get; set; } = true;
    public bool ShareHealth { get; set; }
    public bool ShareReports { get; set; }
}
