using SchoolSync.Domain.Entities;

namespace SchoolSync.Application.Communication;

// The caller as P3's services see it — role and institution from the JWT, never from the body.
public record CommunicationCaller(Guid UserId, UserRole Role, Guid InstitutionId)
{
    public bool IsLeadership => Role is UserRole.Administrator or UserRole.Principal or UserRole.SchoolInCharge;
}

public record AnnouncementFields(string? Title, string? Content, DateOnly? EffectiveDate);

public record AnnouncementResult(
    Guid Id, string Title, string Content, DateOnly EffectiveDate, Guid AuthorId, DateTime CreatedAtUtc,
    bool IsWithdrawn, DateTime? WithdrawnAtUtc);

public enum CommunicationFailureReason
{
    None,
    ValidationFailed, // ERR-901
    NotFound, // ERR-902
    Forbidden, // ERR-102
    WithdrawLeadershipOnly, // ERR-501
    WrongPasscode, // ERR-502
    RoomRemovalForbidden, // ERR-503
    MessageTooLong, // ERR-504
    NotRoomMember, // ERR-505
    MeetingsUnavailable // ERR-903 — ACS not configured in this environment (Q-221)
}

public record CommunicationResult<T>(bool Succeeded, T? Value, CommunicationFailureReason Failure = CommunicationFailureReason.None)
{
    public static CommunicationResult<T> Ok(T value) => new(true, value);
    public static CommunicationResult<T> Fail(CommunicationFailureReason reason) => new(false, default, reason);
}

public interface IAnnouncementService
{
    Task<CommunicationResult<AnnouncementResult>> CreateAsync(AnnouncementFields fields, CommunicationCaller caller, CancellationToken ct); // API-037
    Task<CommunicationResult<AnnouncementResult>> AmendAsync(Guid id, AnnouncementFields fields, CommunicationCaller caller, CancellationToken ct); // API-038
    Task<CommunicationResult<AnnouncementResult>> WithdrawAsync(Guid id, CommunicationCaller caller, CancellationToken ct); // API-039
    Task<IReadOnlyList<AnnouncementResult>> ListCurrentAsync(CancellationToken ct); // API-040
    Task<AnnouncementResult?> FindAsync(Guid id, CancellationToken ct); // API-040
}

// Tenant-filtered throughout (Announcement is ITenantOwned): BR-036's "publishing institution
// only" is structural, not a WHERE each query has to remember.
public interface IAnnouncementRepository
{
    Task AddAsync(Announcement announcement, CancellationToken ct);
    Task<Announcement?> FindAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Announcement>> ListNotWithdrawnAsync(CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
