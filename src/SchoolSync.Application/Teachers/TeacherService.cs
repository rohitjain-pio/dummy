using SchoolSync.Application.Validation;
using SchoolSync.Domain.Common;
using SchoolSync.Domain.Entities;

namespace SchoolSync.Application.Teachers;

// API-020 – API-025. BRL-022 through BRL-025.
public class TeacherService(ITeacherRepository repository, TimeProvider clock) : ITeacherService
{
    // API-020. BRL-022: a teacher belongs to exactly one institution, which is the caller's —
    // taken from the claim, never from the payload.
    public async Task<TeacherOutcome<TeacherResult>> RegisterAsync(TeacherFields fields, Guid institutionId, CancellationToken ct)
    {
        if (!IsWellFormed(fields)) return TeacherOutcome<TeacherResult>.Fail(TeacherFailureReason.ValidationFailed);

        var teacher = new Teacher
        {
            Id = Guid.NewGuid(),
            InstitutionId = institutionId,
            Name = fields.Name,
            ContactEmail = fields.ContactEmail,
            Telephone = fields.Telephone,
            Gender = fields.Gender,
            Address = fields.Address,
            JoiningDate = fields.JoiningDate,
            IsActive = true
        };

        await repository.AddAsync(teacher, ct);
        await repository.SaveChangesAsync(ct);
        return TeacherOutcome<TeacherResult>.Ok(ToResult(teacher));
    }

    // API-021.
    public async Task<TeacherOutcome<TeacherResult>> AmendAsync(Guid id, TeacherFields fields, CancellationToken ct)
    {
        var teacher = await repository.FindByIdAsync(id, ct);
        if (teacher is null) return TeacherOutcome<TeacherResult>.Fail(TeacherFailureReason.NotFound);

        if (!IsWellFormed(fields)) return TeacherOutcome<TeacherResult>.Fail(TeacherFailureReason.ValidationFailed);

        teacher.Name = fields.Name;
        teacher.ContactEmail = fields.ContactEmail;
        teacher.Telephone = fields.Telephone;
        teacher.Gender = fields.Gender;
        teacher.Address = fields.Address;
        teacher.JoiningDate = fields.JoiningDate;

        await repository.SaveChangesAsync(ct);
        return TeacherOutcome<TeacherResult>.Ok(ToResult(teacher));
    }

    // API-022. BRL-025 / E2 / TC-304: the guard runs BEFORE any state change, and on rejection
    // the teacher must be left active — a version that deactivated first and then reported the
    // conflict would satisfy the error code while orphaning exactly the assignment BR-025 exists
    // to protect.
    public async Task<TeacherOutcome<TeacherResult>> RemoveAsync(Guid id, CancellationToken ct)
    {
        var teacher = await repository.FindByIdAsync(id, ct);
        if (teacher is null) return TeacherOutcome<TeacherResult>.Fail(TeacherFailureReason.NotFound);

        if (await repository.HasActiveAssignmentsAsync(id, ct))
            return TeacherOutcome<TeacherResult>.Fail(TeacherFailureReason.HasActiveAssignments);

        teacher.Deactivate(clock.GetUtcNow().UtcDateTime); // D8, via CT-004's canonical path
        await repository.SaveChangesAsync(ct);
        return TeacherOutcome<TeacherResult>.Ok(ToResult(teacher));
    }

    // SPEC-FR-019 calls removal "reversible", so the mirror exists for the same reason CT-101's
    // and CT-102's do.
    public async Task<TeacherOutcome<TeacherResult>> ReinstateAsync(Guid id, CancellationToken ct)
    {
        var teacher = await repository.FindByIdAsync(id, ct);
        if (teacher is null) return TeacherOutcome<TeacherResult>.Fail(TeacherFailureReason.NotFound);

        teacher.Reactivate();
        await repository.SaveChangesAsync(ct);
        return TeacherOutcome<TeacherResult>.Ok(ToResult(teacher));
    }

    // SPEC-FR-023. The link is held on UserAccount.TeacherId, mirroring CT-102's API-015.
    public async Task<TeacherOutcome<TeacherResult>> LinkAccountAsync(Guid id, Guid userAccountId, CancellationToken ct)
    {
        var teacher = await repository.FindByIdAsync(id, ct);
        if (teacher is null) return TeacherOutcome<TeacherResult>.Fail(TeacherFailureReason.NotFound);

        var account = await repository.FindUserAccountAsync(userAccountId, ct);
        if (account is null) return TeacherOutcome<TeacherResult>.Fail(TeacherFailureReason.NotFound);

        account.TeacherId = teacher.Id;
        await repository.SaveChangesAsync(ct);
        return TeacherOutcome<TeacherResult>.Ok(ToResult(teacher));
    }

    public async Task<TeacherResult?> FindAsync(Guid id, CancellationToken ct)
    {
        var teacher = await repository.FindByIdAsync(id, ct);
        return teacher is null ? null : ToResult(teacher);
    }

    public async Task<IReadOnlyList<TeacherResult>> ListAsync(CancellationToken ct) =>
        (await repository.ListAsync(ct)).Select(ToResult).ToList();

    // API-024. BRL-023 has TWO clauses and ERR-303's wording names both: "This class already has
    // a class teacher, OR this teacher is already a class teacher elsewhere."
    //
    // RESOLVED SILENCE (recorded in the build log): BRL-023 says the second assignment is rejected
    // "until the first is released", so a release path must exist, but the contract gives only
    // this one endpoint taking {teacherId}. A null teacherId is therefore read as the release.
    // Overwriting an occupied class's teacher directly is NOT treated as an implicit release —
    // that is precisely the case ERR-303's first clause names.
    public async Task<TeacherOutcome<bool>> SetClassTeacherAsync(Guid classId, Guid? teacherId, CancellationToken ct)
    {
        var @class = await repository.FindClassAsync(classId, ct);
        if (@class is null) return TeacherOutcome<bool>.Fail(TeacherFailureReason.NotFound);

        // Release.
        if (teacherId is null)
        {
            @class.ClassTeacherId = null;
            await repository.SaveChangesAsync(ct);
            return TeacherOutcome<bool>.Ok(true);
        }

        var teacher = await repository.FindByIdAsync(teacherId.Value, ct);
        if (teacher is null) return TeacherOutcome<bool>.Fail(TeacherFailureReason.NotFound);

        // Idempotent: the requested post-condition already holds, so this is not "a second
        // assignment" and there is nothing to reject.
        if (@class.ClassTeacherId == teacherId) return TeacherOutcome<bool>.Ok(true);

        // Clause 1 — the class is already occupied by someone else. Release it first.
        if (@class.ClassTeacherId is not null)
            return TeacherOutcome<bool>.Fail(TeacherFailureReason.AssignmentConflict);

        // Clause 2 — this teacher already holds another class. This is the half TC-303 targets,
        // and the half an implementation that only looked at the class row would miss entirely.
        if (await repository.IsClassTeacherElsewhereAsync(teacherId.Value, classId, ct))
            return TeacherOutcome<bool>.Fail(TeacherFailureReason.AssignmentConflict);

        @class.ClassTeacherId = teacherId;
        await repository.SaveChangesAsync(ct);
        return TeacherOutcome<bool>.Ok(true);
    }

    // API-025. BRL-024: a teacher MAY hold multiple (subject, class) pairs — so unlike BRL-023
    // there is deliberately no uniqueness guard here beyond the composite key itself.
    public async Task<TeacherOutcome<SubjectAssignment>> AssignSubjectAsync(
        Guid subjectId, Guid teacherId, Guid classId, CancellationToken ct)
    {
        if (!await repository.SubjectExistsAsync(subjectId, ct))
            return TeacherOutcome<SubjectAssignment>.Fail(TeacherFailureReason.NotFound);

        var teacher = await repository.FindByIdAsync(teacherId, ct);
        if (teacher is null) return TeacherOutcome<SubjectAssignment>.Fail(TeacherFailureReason.NotFound);

        if (await repository.FindClassAsync(classId, ct) is null)
            return TeacherOutcome<SubjectAssignment>.Fail(TeacherFailureReason.NotFound);

        // Re-submitting an existing mapping is a no-op rather than a composite-PK violation.
        if (!await repository.SubjectAssignmentExistsAsync(subjectId, teacherId, classId, ct))
        {
            await repository.AddSubjectAssignmentAsync(new SubjectTeacherClassMapping
            {
                SubjectId = subjectId,
                TeacherId = teacherId,
                ClassId = classId
            }, ct);
            await repository.SaveChangesAsync(ct);
        }

        return TeacherOutcome<SubjectAssignment>.Ok(new SubjectAssignment(subjectId, teacherId, classId));
    }

    // No VAL- rule is declared for teacher fields anywhere in the spec suite (07-business-logic/
    // academic-records.md declares VAL-008–012, all student/code). This applies company.md §3's
    // org-level "validate and encode all external input" baseline using the same contact-format
    // helpers as VAL-001/VAL-003 — it is the org floor being honoured where the spec is silent,
    // not a new business rule being invented.
    private static bool IsWellFormed(TeacherFields fields) =>
        !string.IsNullOrWhiteSpace(fields.Name)
        && !string.IsNullOrWhiteSpace(fields.Gender)
        && !string.IsNullOrWhiteSpace(fields.Address)
        && ContactFormats.IsValidEmail(fields.ContactEmail)
        && ContactFormats.IsValidPhone(fields.Telephone);

    private static TeacherResult ToResult(Teacher t) => new(
        t.Id, t.InstitutionId, t.Name, t.ContactEmail, t.Telephone, t.Gender, t.Address,
        t.JoiningDate, t.IsActive, t.DeactivatedAtUtc);
}
