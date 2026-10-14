using SchoolSync.Application.ParentLinks;
using SchoolSync.Domain.Entities;
using SchoolSync.IntegrationTests.Fakes;
using Xunit;

namespace SchoolSync.IntegrationTests.ParentLinks;

// 14-test-spec/academic-records.md TC-305, TC-306, TC-307 (all Layer: Unit), plus the rest of
// T-003's state machine, which CT-103's Definition of Done requires be adversarially tested in
// full: Issued -> Consumed / Revoked / Expired, and the regenerate edge.
public class ParentLinkServiceTests
{
    private static readonly Guid InstitutionId = Guid.NewGuid();
    private static readonly Guid StudentId = Guid.NewGuid();
    private static readonly Guid StaffUserId = Guid.NewGuid();
    private static readonly Guid ParentUserId = Guid.NewGuid();

    private readonly TestClock _clock = new();
    private readonly FakeParentLinkRepository _repository = new();
    private readonly ParentLinkService _service;

    public ParentLinkServiceTests()
    {
        _repository.Students.Add(new Student
        {
            Id = StudentId,
            InstitutionId = InstitutionId,
            SrNumber = "SR-1",
            RollNumber = "ROLL-1",
            Name = "Asha Menon",
            DateOfBirth = new DateOnly(2016, 4, 2),
            Gender = "Female",
            Email = "asha@example.edu"
        });
        _repository.StudentInstitutions[StudentId] = InstitutionId;
        _service = new ParentLinkService(_repository, new ParentLinkCodeGenerator(), _clock);
    }

    private async Task<string> IssueAsync()
    {
        var issued = await _service.IssueCodeAsync(StudentId, StaffUserId, CancellationToken.None);
        Assert.True(issued.Succeeded);
        return issued.Value!.Code;
    }

    [Fact] // TC-305 — PF-005 happy path
    public async Task A_valid_unconsumed_unexpired_code_from_the_same_institution_creates_the_link()
    {
        var code = await IssueAsync();

        var result = await _service.ConsumeCodeAsync(code, ParentUserId, InstitutionId, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(StudentId, result.Value!.StudentId);

        var link = Assert.Single(_repository.Links);
        Assert.Equal(ParentUserId, link.ParentUserId);
        Assert.Equal(StudentId, link.StudentId);

        // "code marked consumed" is half of TC-305's Then — the link alone is not enough.
        var stored = Assert.Single(_repository.Codes);
        Assert.NotNull(stored.ConsumedAtUtc);
        Assert.Equal(ParentUserId, stored.ConsumedByUserId);
    }

    [Fact] // TC-306 — adversarial: fails against an implementation with no consumed-state check
    public async Task A_code_that_has_already_been_consumed_cannot_be_consumed_again()
    {
        var code = await IssueAsync();
        var first = await _service.ConsumeCodeAsync(code, ParentUserId, InstitutionId, CancellationToken.None);
        Assert.True(first.Succeeded);

        var second = await _service.ConsumeCodeAsync(code, Guid.NewGuid(), InstitutionId, CancellationToken.None);

        Assert.False(second.Succeeded);
        Assert.Equal(ParentLinkFailureReason.NotValid, second.Failure); // ERR-310
        Assert.Single(_repository.Links); // and no second link was created
    }

    [Fact] // TC-307 — adversarial: fails against an implementation with no expiry check
    public async Task A_code_issued_eight_days_ago_is_rejected_as_expired()
    {
        var code = await IssueAsync();

        _clock.Advance(TimeSpan.FromDays(8)); // CFG-005 allows 7

        var result = await _service.ConsumeCodeAsync(code, ParentUserId, InstitutionId, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ParentLinkFailureReason.Expired, result.Failure); // ERR-308
        Assert.Empty(_repository.Links);
    }

    [Theory] // The CFG-005 boundary itself — proves the window is 7 days, not merely "some window"
    [InlineData(6, true)]
    [InlineData(7, false)] // expires_at = issued_at + 7d, and the check is `now >= expires_at`
    public async Task The_expiry_window_is_exactly_seven_days(int daysLater, bool expectedToSucceed)
    {
        var code = await IssueAsync();

        _clock.Advance(TimeSpan.FromDays(daysLater));

        var result = await _service.ConsumeCodeAsync(code, ParentUserId, InstitutionId, CancellationToken.None);

        Assert.Equal(expectedToSucceed, result.Succeeded);
    }

    [Fact] // Decision table row 3 at the unit layer (TC-308 covers it end-to-end)
    public async Task A_code_belonging_to_another_institutions_student_is_rejected_as_a_mismatch()
    {
        var code = await IssueAsync();

        var result = await _service.ConsumeCodeAsync(code, ParentUserId, Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ParentLinkFailureReason.InstitutionMismatch, result.Failure); // ERR-309
        Assert.Empty(_repository.Links);
    }

    [Fact] // Row order is load-bearing: an EXPIRED foreign code is ERR-308, never ERR-309
    public async Task Expiry_is_decided_before_institution_match()
    {
        var code = await IssueAsync();
        _clock.Advance(TimeSpan.FromDays(8));

        var result = await _service.ConsumeCodeAsync(code, ParentUserId, Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(ParentLinkFailureReason.Expired, result.Failure);
    }

    [Fact] // Issued -> Revoked (terminal)
    public async Task A_revoked_code_cannot_be_consumed()
    {
        var code = await IssueAsync();
        var codeId = _repository.Codes.Single().Id;

        var revoke = await _service.RevokeCodeAsync(StudentId, codeId, CancellationToken.None);
        Assert.True(revoke.Succeeded);

        var result = await _service.ConsumeCodeAsync(code, ParentUserId, InstitutionId, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ParentLinkFailureReason.NotValid, result.Failure); // ERR-310
    }

    [Fact] // ERR-307: consumption is terminal, so revocation cannot undo it
    public async Task Revoking_an_already_consumed_code_reports_ERR_307()
    {
        var code = await IssueAsync();
        var codeId = _repository.Codes.Single().Id;
        await _service.ConsumeCodeAsync(code, ParentUserId, InstitutionId, CancellationToken.None);

        var result = await _service.RevokeCodeAsync(StudentId, codeId, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ParentLinkFailureReason.AlreadyConsumed, result.Failure);
    }

    [Fact] // "Issued --[regenerate, before consumption]--> Issued (new code, OLD CODE INVALIDATED)"
    public async Task Reissuing_a_code_invalidates_the_previous_one()
    {
        var firstCode = await IssueAsync();
        var secondCode = await IssueAsync();

        Assert.NotEqual(firstCode, secondCode);

        // The old code must now be dead — an implementation that merely adds a second row without
        // invalidating the first leaves two live codes and passes a naive "new code works" test.
        var oldResult = await _service.ConsumeCodeAsync(firstCode, ParentUserId, InstitutionId, CancellationToken.None);
        Assert.False(oldResult.Succeeded);
        Assert.Equal(ParentLinkFailureReason.NotValid, oldResult.Failure);

        var newResult = await _service.ConsumeCodeAsync(secondCode, ParentUserId, InstitutionId, CancellationToken.None);
        Assert.True(newResult.Succeeded);
    }

    [Fact] // E1: withdrawal does not auto-revoke an outstanding code
    public async Task Withdrawing_the_student_does_not_invalidate_an_outstanding_code()
    {
        var code = await IssueAsync();
        _repository.Students.Single().IsActive = false; // withdrawn, but nothing revokes the code

        var result = await _service.ConsumeCodeAsync(code, ParentUserId, InstitutionId, CancellationToken.None);

        Assert.True(result.Succeeded); // "Code remains valid/consumable"
    }

    [Fact] // Staff cannot issue against a student they cannot see
    public async Task Issuing_against_an_unknown_student_is_rejected()
    {
        var result = await _service.IssueCodeAsync(Guid.NewGuid(), StaffUserId, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ParentLinkFailureReason.NotFound, result.Failure);
    }

    [Fact] // An unknown code is ERR-310, never a crash or a 404
    public async Task An_unknown_code_is_rejected_as_not_valid()
    {
        var result = await _service.ConsumeCodeAsync("ZZZZZZZZ", ParentUserId, InstitutionId, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ParentLinkFailureReason.NotValid, result.Failure);
    }

    [Fact] // VAL-012 / CFG-005: exactly 8 characters, from the unambiguous alphabet
    public async Task An_issued_code_is_eight_characters_and_avoids_confusable_glyphs()
    {
        var code = await IssueAsync();

        Assert.Equal(8, code.Length);
        // ToCharArray so this binds to the collection+predicate overload rather than the
        // string/substring one.
        Assert.DoesNotContain(code.ToCharArray(), c => c is 'O' or '0' or 'I' or 'L' or '1');
    }
}
