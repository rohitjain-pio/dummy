using SchoolSync.Domain.Entities;

namespace SchoolSync.Api.Authorization;

// The role sets the API contracts name in prose ("Auth: leadership", "leadership + Teacher
// (read)"), expressed once. 02-requirements-spec/attendance.md SPEC-FR-032 spells the leadership
// set out literally as {Administrator, Principal, SchoolInCharge}, and BR-010 grants exactly
// those three full CRUD within their institution.
//
// nameof() is a constant expression, so these concatenations are compile-time constants and are
// therefore legal in an [Authorize(Roles = ...)] attribute argument — which is the whole reason
// for the string-concatenation shape rather than a string[] or an enum array.
public static class Roles
{
    public const string Leadership =
        nameof(UserRole.Administrator) + "," +
        nameof(UserRole.Principal) + "," +
        nameof(UserRole.SchoolInCharge);

    // API-014/API-028: Teacher is granted READ access alongside leadership. FR-027's correction
    // (ui-design-final.md §0 correction 1) is why Teacher appears here at all.
    public const string LeadershipAndTeacher = Leadership + "," + nameof(UserRole.Teacher);
}
