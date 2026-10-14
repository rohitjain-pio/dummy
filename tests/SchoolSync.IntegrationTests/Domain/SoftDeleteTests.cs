using System.Text.RegularExpressions;
using SchoolSync.Domain.Common;
using SchoolSync.Domain.Entities;
using Xunit;

namespace SchoolSync.IntegrationTests.Domain;

// CT-004: BR-005/D8 (07-business-logic/_shared.md §2) — the reversible-deactivation pattern
// applied to Institution, Student, Teacher, Room, UserAccount.
public class SoftDeleteTests
{
    // "Reactivate mirrors Deactivate exactly" is checked against all five named entities, not
    // just one representative — a convention that happened to be wired correctly for Student but
    // not for Room (the entity CT-004 itself had to add the columns to) is exactly the kind of
    // gap a single-entity test would miss.
    public static IEnumerable<object[]> AllFiveSoftDeletableEntities()
    {
        yield return [new Institution
        {
            Id = Guid.NewGuid(), Name = "N", RegistrationNumber = "R", ContactEmail = "e@e.com",
            Telephone = "T", PostalAddress = "A", PostalCode = "000000", City = "C", Region = "R",
            SubscriptionPlanId = Guid.NewGuid(), CreatedAtUtc = DateTime.UtcNow
        }];
        yield return [new Student
        {
            Id = Guid.NewGuid(), InstitutionId = Guid.NewGuid(), SrNumber = "SR-1", RollNumber = "1",
            Name = "N", DateOfBirth = new DateOnly(2010, 1, 1), Gender = "G", Email = "e@e.com"
        }];
        yield return [new Teacher
        {
            Id = Guid.NewGuid(), InstitutionId = Guid.NewGuid(), Name = "N", ContactEmail = "e@e.com",
            Telephone = "T", Gender = "G", Address = "A", JoiningDate = new DateOnly(2020, 1, 1)
        }];
        yield return [new Room
        {
            Id = Guid.NewGuid(), InstitutionId = Guid.NewGuid(), Name = "N", PasscodeHash = "H",
            CreatedByUserId = Guid.NewGuid()
        }];
        yield return [new UserAccount
        {
            Id = Guid.NewGuid(), InstitutionId = Guid.NewGuid(), Username = "u", Email = "e@e.com",
            PasswordHash = "H", Role = UserRole.Teacher
        }];
    }

    [Theory]
    [MemberData(nameof(AllFiveSoftDeletableEntities))]
    public void Deactivate_then_Reactivate_restores_exactly_the_original_state(ISoftDeletable entity)
    {
        Assert.True(entity.IsActive);
        Assert.Null(entity.DeactivatedAtUtc);

        entity.Deactivate(DateTime.UtcNow);
        Assert.False(entity.IsActive);
        Assert.NotNull(entity.DeactivatedAtUtc);

        entity.Reactivate();

        // The adversarial assertion: not just "IsActive is true again" (which a buggy
        // Reactivate() that also mutated some OTHER field would still pass) but that the
        // entity's soft-delete state is back to bit-for-bit what Deactivate() started from.
        Assert.True(entity.IsActive);
        Assert.Null(entity.DeactivatedAtUtc);
    }

    [Fact]
    public void No_production_source_file_calls_Remove_on_a_soft_deletable_DbSet()
    {
        // "No physical delete path exists for these five entity types anywhere in the API
        // surface" (07-business-logic/_shared.md §2) — verified mechanically, the same pattern
        // CT-003 used for ExecuteUpdate/ExecuteDelete: if a future CT ever adds
        // `db.Institutions.Remove(...)` (or the other four), this test fails and names the file,
        // instead of the gap surviving until a real DELETE silently destroys history.
        var repoRoot = FindRepoRoot();
        var srcDir = Path.Combine(repoRoot, "src");
        Assert.True(Directory.Exists(srcDir), $"expected to find src/ under {repoRoot}");

        var softDeletableDbSetNames = new[] { "Institutions", "Students", "Teachers", "Rooms", "UserAccounts" };
        var pattern = new Regex(
            $@"\.(?:{string.Join("|", softDeletableDbSetNames)})\s*\.\s*Remove(Range)?\s*\(",
            RegexOptions.Compiled);

        var offendingFiles = Directory.EnumerateFiles(srcDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => pattern.IsMatch(File.ReadAllText(f)))
            .ToList();

        Assert.True(offendingFiles.Count == 0,
            "Found a hard-delete (.Remove/.RemoveRange) call against a soft-deletable DbSet, which " +
            $"07-business-logic/_shared.md §2 says must never be reachable: {string.Join(", ", offendingFiles)}");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SchoolSync.slnx")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("Could not locate repo root from test output directory.");
    }
}
