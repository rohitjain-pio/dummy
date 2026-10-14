using SchoolSync.Application.Students;
using SchoolSync.Domain.Entities;
using SchoolSync.IntegrationTests.Fakes;
using Xunit;

namespace SchoolSync.IntegrationTests.Students;

// 14-test-spec/academic-records.md TC-302 (Layer: Unit), plus the checks that keep TC-302 honest.
public class StudentServiceTests
{
    private static readonly Guid InstitutionId = Guid.NewGuid();
    private static readonly Guid ClassId = Guid.NewGuid();

    private static DateOnly PlausibleDob => DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-10);

    private static StudentFields Fields(string srNumber, string rollNumber) => new(
        srNumber, rollNumber, "Asha Menon", PlausibleDob, "Female", "asha.menon@example.edu",
        ClassId, null, null, null, null);

    private static (StudentService Service, FakeStudentRepository Repository) Build(params Student[] existing)
    {
        var repository = new FakeStudentRepository();
        repository.ExistingClassIds.Add(ClassId);
        repository.Students.AddRange(existing);
        // CT-503 added the tier gate to StudentService; these P1 tests are not about tiers.
        return (new StudentService(repository, new StudentFieldPolicy(), TimeProvider.System, new SchoolSync.IntegrationTests.Fakes.UnlimitedPlanEntitlements(), new SchoolSync.IntegrationTests.Fakes.PassthroughTransaction()), repository);
    }

    private static Student Existing(string srNumber, string rollNumber, bool isActive = true) => new()
    {
        Id = Guid.NewGuid(),
        InstitutionId = InstitutionId,
        ClassId = ClassId,
        SrNumber = srNumber,
        RollNumber = rollNumber,
        Name = "Existing Student",
        DateOfBirth = PlausibleDob,
        Gender = "Male",
        Email = "existing@example.edu",
        IsActive = isActive
    };

    [Fact] // TC-302 — adversarial: fails against an implementation that checks roll number only
    public async Task Student_reusing_an_existing_SR_number_is_rejected_even_when_the_roll_number_is_free()
    {
        var (service, _) = Build(Existing(srNumber: "SR-1000", rollNumber: "ROLL-1"));

        // Same SR number, deliberately DIFFERENT roll number. An implementation that checks only
        // the roll number finds nothing in its way here and returns a created student.
        var result = await service.EnrolAsync(Fields("SR-1000", "ROLL-2"), InstitutionId, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(StudentFailureReason.DuplicateIdentifier, result.Failure);
    }

    [Fact] // TC-302's mirror — the same check from the other side, so neither half can rot unnoticed
    public async Task Student_reusing_an_existing_roll_number_is_rejected_even_when_the_SR_number_is_free()
    {
        var (service, _) = Build(Existing(srNumber: "SR-1000", rollNumber: "ROLL-1"));

        var result = await service.EnrolAsync(Fields("SR-2000", "ROLL-1"), InstitutionId, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(StudentFailureReason.DuplicateIdentifier, result.Failure);
    }

    [Fact] // Positive control: without this, both tests above would still pass if enrolment always failed
    public async Task Student_with_both_identifiers_free_is_enrolled()
    {
        var (service, repository) = Build(Existing(srNumber: "SR-1000", rollNumber: "ROLL-1"));

        var result = await service.EnrolAsync(Fields("SR-2000", "ROLL-2"), InstitutionId, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(InstitutionId, result.Value!.InstitutionId);
        Assert.Equal(ClassId, result.Value.ClassId);
        Assert.Equal(2, repository.Students.Count);
    }

    [Fact] // BRL-018 against a WITHDRAWN student — the DB unique index has no IsActive filter
    public async Task Withdrawn_students_still_hold_their_SR_number_against_a_new_enrolment()
    {
        var (service, _) = Build(Existing(srNumber: "SR-1000", rollNumber: "ROLL-1", isActive: false));

        var result = await service.EnrolAsync(Fields("SR-1000", "ROLL-9"), InstitutionId, CancellationToken.None);

        // An implementation that quietly skips inactive rows would accept this and then hit a
        // unique-constraint violation at SaveChanges against a real database.
        Assert.False(result.Succeeded);
        Assert.Equal(StudentFailureReason.DuplicateIdentifier, result.Failure);
    }

    [Fact] // SPEC-FR-011: "class must exist in caller's institution"
    public async Task Enrolment_naming_a_class_that_does_not_exist_is_rejected()
    {
        var (service, _) = Build();

        var fields = Fields("SR-3000", "ROLL-3") with { ClassId = Guid.NewGuid() };
        var result = await service.EnrolAsync(fields, InstitutionId, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(StudentFailureReason.ClassNotFound, result.Failure);
    }

    [Fact] // BRL-018 on amend — a student must not collide with ITSELF
    public async Task Amending_a_student_without_changing_its_identifiers_is_not_a_duplicate()
    {
        var existing = Existing(srNumber: "SR-1000", rollNumber: "ROLL-1");
        var (service, _) = Build(existing);

        var result = await service.AmendAsync(
            existing.Id, Fields("SR-1000", "ROLL-1") with { Name = "Renamed Student" }, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("Renamed Student", result.Value!.Name);
    }

    [Fact] // BRL-017: reassignment REPLACES the placement rather than adding a second
    public async Task Changing_class_placement_replaces_the_previous_one()
    {
        var existing = Existing(srNumber: "SR-1000", rollNumber: "ROLL-1");
        var (service, repository) = Build(existing);
        var newClassId = Guid.NewGuid();
        repository.ExistingClassIds.Add(newClassId);

        var result = await service.AmendAsync(
            existing.Id, Fields("SR-1000", "ROLL-1") with { ClassId = newClassId }, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(newClassId, result.Value!.ClassId);
        Assert.Equal(newClassId, repository.Students.Single().ClassId); // exactly one placement, the new one
    }

    [Fact] // SPEC-FR-014: withdrawal is reversible and does not erase the record
    public async Task Withdraw_then_reactivate_restores_the_student()
    {
        var existing = Existing(srNumber: "SR-1000", rollNumber: "ROLL-1");
        var (service, _) = Build(existing);

        var withdrawn = await service.WithdrawAsync(existing.Id, CancellationToken.None);
        Assert.True(withdrawn.Succeeded);
        Assert.False(withdrawn.Value!.IsActive);
        Assert.NotNull(withdrawn.Value.DeactivatedAtUtc);

        var reactivated = await service.ReactivateAsync(existing.Id, CancellationToken.None);
        Assert.True(reactivated.Succeeded);
        Assert.True(reactivated.Value!.IsActive);
        Assert.Null(reactivated.Value.DeactivatedAtUtc);
        Assert.Equal("SR-1000", reactivated.Value.SrNumber); // record intact, not recreated
    }
}
