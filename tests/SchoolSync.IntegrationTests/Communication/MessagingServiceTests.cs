using Microsoft.EntityFrameworkCore;
using SchoolSync.Application.Communication;
using SchoolSync.Domain.Entities;
using SchoolSync.Infrastructure.Communication;
using SchoolSync.Infrastructure.Persistence;
using SchoolSync.IntegrationTests.Fakes;
using Xunit;

namespace SchoolSync.IntegrationTests.Communication;

// CT-303 Unit-layer rows of 14-test-spec/communication.md: TC-504 and TC-510, plus BRL-044's
// author-only rule, EVT-001 idempotency and the Q-219 leave. Service over its real repository and
// an EF InMemory context — "row not deleted" is a claim about storage, not about a fake.
public class MessagingServiceTests : IDisposable
{
    private static readonly Guid InstitutionId = Guid.NewGuid();
    private static readonly Guid RoomId = Guid.NewGuid();
    private static readonly Guid AuthorUserId = Guid.NewGuid();
    private static readonly Guid OtherMemberUserId = Guid.NewGuid();

    private readonly SchoolSyncDbContext _db;
    private readonly MessagingService _service;
    private readonly CommunicationCaller _author = new(AuthorUserId, UserRole.Student, InstitutionId);
    private readonly CommunicationCaller _otherMember = new(OtherMemberUserId, UserRole.Principal, InstitutionId);

    public MessagingServiceTests()
    {
        var options = new DbContextOptionsBuilder<SchoolSyncDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _db = new SchoolSyncDbContext(options, new FakeCurrentTenant { InstitutionId = InstitutionId });
        _db.Rooms.Add(new Room { Id = RoomId, InstitutionId = InstitutionId, Name = "Science", PasscodeHash = "x", CreatedByUserId = AuthorUserId });
        _db.RoomMemberships.AddRange(
            new RoomMembership { RoomId = RoomId, UserId = AuthorUserId, InstitutionId = InstitutionId, JoinedAtUtc = DateTime.UtcNow },
            new RoomMembership { RoomId = RoomId, UserId = OtherMemberUserId, InstitutionId = InstitutionId, JoinedAtUtc = DateTime.UtcNow });
        _db.SaveChanges();
        _service = new MessagingService(new MessagingRepository(_db), new TestClock());
    }

    public void Dispose() => _db.Dispose();

    private Task<CommunicationResult<SentMessage>> SendAsync(string content, Guid? clientId = null, CommunicationCaller? caller = null) =>
        _service.SendAsync(RoomId, clientId ?? Guid.NewGuid(), content, caller ?? _author, CancellationToken.None);

    [Fact] // TC-504 — adversarial boundary: 4,001 rejected, 4,000 accepted
    public async Task A_message_of_4001_characters_is_rejected_with_ERR_504_and_4000_is_accepted()
    {
        var over = await SendAsync(new string('x', 4001));
        var atLimit = await SendAsync(new string('x', 4000));

        Assert.Equal(CommunicationFailureReason.MessageTooLong, over.Failure);
        Assert.True(atLimit.Succeeded);
        Assert.Single(await _db.Messages.ToListAsync()); // the rejected one was never stored
    }

    [Theory] // VAL-020: at least one character
    [InlineData("")]
    [InlineData("   ")]
    public async Task Empty_messages_are_rejected(string content) =>
        Assert.Equal(CommunicationFailureReason.ValidationFailed, (await SendAsync(content)).Failure);

    [Fact] // TC-510 — adversarial: fails against an implementation that deletes the row
    public async Task A_withdrawn_message_keeps_its_row_and_author_and_renders_with_content_blanked()
    {
        var sent = (await SendAsync("Meet at 4pm")).Value!;

        var withdrawn = await _service.WithdrawAsync(RoomId, sent.Message.Id, _author, CancellationToken.None);

        Assert.True(withdrawn.Value!.IsWithdrawn);
        Assert.Equal(string.Empty, withdrawn.Value.Content); // rendered blank
        Assert.Equal(AuthorUserId, withdrawn.Value.AuthorId); // author still shown

        _db.ChangeTracker.Clear();
        var row = Assert.Single(await _db.Messages.ToListAsync()); // row NOT deleted
        Assert.True(row.IsWithdrawn);
        Assert.NotNull(row.WithdrawnAtUtc);
        Assert.Equal("Meet at 4pm", row.Content); // retained for accountability, blanked only on read
    }

    [Fact] // BRL-044: revise/withdraw are the author's alone — not another member, not leadership
    public async Task Only_the_author_may_revise_or_withdraw()
    {
        var sent = (await SendAsync("Original")).Value!;

        Assert.Equal(CommunicationFailureReason.Forbidden, (await _service.ReviseAsync(RoomId, sent.Message.Id, "Hijacked", _otherMember, CancellationToken.None)).Failure);
        Assert.Equal(CommunicationFailureReason.Forbidden, (await _service.WithdrawAsync(RoomId, sent.Message.Id, _otherMember, CancellationToken.None)).Failure);

        var revised = await _service.ReviseAsync(RoomId, sent.Message.Id, "Original, corrected", _author, CancellationToken.None);
        Assert.Equal("Original, corrected", revised.Value!.Content);
        Assert.NotNull(revised.Value.RevisedAtUtc);
    }

    [Fact] // A withdrawn message cannot be revised back into view
    public async Task A_withdrawn_message_cannot_be_revised()
    {
        var sent = (await SendAsync("Oops")).Value!;
        await _service.WithdrawAsync(RoomId, sent.Message.Id, _author, CancellationToken.None);

        var revise = await _service.ReviseAsync(RoomId, sent.Message.Id, "Back again", _author, CancellationToken.None);

        Assert.Equal(CommunicationFailureReason.ValidationFailed, revise.Failure);
    }

    [Fact] // EVT-001: a repeated clientMessageId is a no-op returning the original message
    public async Task Retrying_with_the_same_client_message_id_does_not_create_a_duplicate()
    {
        var clientId = Guid.NewGuid();
        var first = (await SendAsync("Hello", clientId)).Value!;

        var retry = (await SendAsync("Hello", clientId)).Value!;

        Assert.True(retry.WasDuplicate);
        Assert.Equal(first.Message.Id, retry.Message.Id);
        Assert.Single(await _db.Messages.ToListAsync());

        // The same client id from a different author is a different message.
        Assert.False((await SendAsync("Hello", clientId, _otherMember)).Value!.WasDuplicate);
    }

    [Fact] // Membership is re-checked per send: after an explicit leave, sending is refused
    public async Task After_leaving_a_member_can_no_longer_send()
    {
        Assert.True((await _service.LeaveAsync(RoomId, _author, CancellationToken.None)).Value);

        Assert.Equal(CommunicationFailureReason.Forbidden, (await SendAsync("Still here?")).Failure);
        Assert.False((await _service.LeaveAsync(RoomId, _author, CancellationToken.None)).Value); // leaving twice is a no-op
    }
}
