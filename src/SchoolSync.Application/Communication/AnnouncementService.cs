using SchoolSync.Domain.Entities;

namespace SchoolSync.Application.Communication;

// API-037–040 (CT-301). BRL-036–039.
public class AnnouncementService(IAnnouncementRepository repository, TimeProvider clock) : IAnnouncementService
{
    public const int TitleMaxLength = 200; // 04-data-model.md §2.7: nvarchar(200)

    private static bool MayPost(CommunicationCaller caller) => caller.IsLeadership || caller.Role == UserRole.Teacher; // BRL-037

    private static bool IsValid(AnnouncementFields f) =>
        !string.IsNullOrWhiteSpace(f.Title) && f.Title.Trim().Length <= TitleMaxLength // VAL-016
        && !string.IsNullOrWhiteSpace(f.Content) // §2.7: content required
        && f.EffectiveDate is not null; // VAL-017 — future dates allowed

    private static AnnouncementResult ToResult(Announcement a) =>
        new(a.Id, a.Title, a.Content, a.EffectiveDate, a.AuthorId, a.CreatedAtUtc, a.IsWithdrawn, a.WithdrawnAtUtc);

    public async Task<CommunicationResult<AnnouncementResult>> CreateAsync(
        AnnouncementFields fields, CommunicationCaller caller, CancellationToken ct)
    {
        if (!MayPost(caller)) return CommunicationResult<AnnouncementResult>.Fail(CommunicationFailureReason.Forbidden);
        if (!IsValid(fields)) return CommunicationResult<AnnouncementResult>.Fail(CommunicationFailureReason.ValidationFailed);

        var announcement = new Announcement
        {
            Id = Guid.NewGuid(),
            InstitutionId = caller.InstitutionId,
            Title = fields.Title!.Trim(),
            Content = fields.Content!,
            EffectiveDate = fields.EffectiveDate!.Value,
            AuthorId = caller.UserId, // SPEC-FR-039 / BRL-039: set once, here, from the token
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime
        };
        await repository.AddAsync(announcement, ct);
        await repository.SaveChangesAsync(ct);
        return CommunicationResult<AnnouncementResult>.Ok(ToResult(announcement));
    }

    public async Task<CommunicationResult<AnnouncementResult>> AmendAsync(
        Guid id, AnnouncementFields fields, CommunicationCaller caller, CancellationToken ct)
    {
        if (!MayPost(caller)) return CommunicationResult<AnnouncementResult>.Fail(CommunicationFailureReason.Forbidden);
        var announcement = await repository.FindAsync(id, ct);
        if (announcement is null) return CommunicationResult<AnnouncementResult>.Fail(CommunicationFailureReason.NotFound);
        if (!IsValid(fields)) return CommunicationResult<AnnouncementResult>.Fail(CommunicationFailureReason.ValidationFailed);

        // BRL-039: content and effective date only. AuthorId and CreatedAtUtc are never touched.
        announcement.Title = fields.Title!.Trim();
        announcement.Content = fields.Content!;
        announcement.EffectiveDate = fields.EffectiveDate!.Value;
        await repository.SaveChangesAsync(ct);
        return CommunicationResult<AnnouncementResult>.Ok(ToResult(announcement));
    }

    public async Task<CommunicationResult<AnnouncementResult>> WithdrawAsync(Guid id, CommunicationCaller caller, CancellationToken ct)
    {
        // BRL-038 / E1: leadership only, regardless of authorship. Checked before existence, so a
        // Teacher learns nothing about which ids exist.
        if (!caller.IsLeadership) return CommunicationResult<AnnouncementResult>.Fail(CommunicationFailureReason.WithdrawLeadershipOnly);
        var announcement = await repository.FindAsync(id, ct);
        if (announcement is null) return CommunicationResult<AnnouncementResult>.Fail(CommunicationFailureReason.NotFound);

        if (!announcement.IsWithdrawn) // withdrawing twice is a no-op, not an error
        {
            announcement.IsWithdrawn = true;
            announcement.WithdrawnAtUtc = clock.GetUtcNow().UtcDateTime;
            await repository.SaveChangesAsync(ct);
        }
        return CommunicationResult<AnnouncementResult>.Ok(ToResult(announcement));
    }

    public async Task<IReadOnlyList<AnnouncementResult>> ListCurrentAsync(CancellationToken ct) =>
        (await repository.ListNotWithdrawnAsync(ct)).Select(ToResult).ToList();

    // A withdrawn announcement is no longer "current" (FR-042) and is not returned individually either.
    public async Task<AnnouncementResult?> FindAsync(Guid id, CancellationToken ct) =>
        await repository.FindAsync(id, ct) is { IsWithdrawn: false } a ? ToResult(a) : null;
}
