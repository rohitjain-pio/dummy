// Standalone demo-data seeder for local manual/UI testing of SchoolSync.
//
// It talks to the real SQL Server (LocalDB) database using the real EF Core model and the real
// BCrypt password hasher, so the accounts it creates work exactly like ones created through the
// app itself. It does NOT go through the API/DI container, so it needs none of the app's runtime
// secrets (Jwt:SigningKey, Security:LeadershipCodeLookupKey) — it only needs a connection string.
//
// Usage:
//   dotnet run --project tools/DbSeeder
//   dotnet run --project tools/DbSeeder -- "<custom connection string>"
//
// Safe to re-run: if the demo institution already exists, it prints the credentials again and
// exits without creating duplicates.

using Microsoft.EntityFrameworkCore;
using SchoolSync.Domain.Entities;
using SchoolSync.Infrastructure.Auth;
using SchoolSync.Infrastructure.Persistence;

const string DemoRegistrationNumber = "DEMO-0001";
const string DefaultConnectionString =
    "Server=(localdb)\\MSSQLLocalDB;Database=SchoolSync;Trusted_Connection=True;TrustServerCertificate=True;";

var connectionString = args.Length > 0 ? args[0] : DefaultConnectionString;

var optionsBuilder = new DbContextOptionsBuilder<SchoolSyncDbContext>();
optionsBuilder.UseSqlServer(connectionString);

var currentTenant = new SeedCurrentTenant();
await using var db = new SchoolSyncDbContext(optionsBuilder.Options, currentTenant);

Console.WriteLine("Applying any pending EF Core migrations...");
await db.Database.MigrateAsync();

var hasher = new BCryptPasswordHasher();

var credentials = new List<(string Role, string Username, string Email, string Password)>
{
    ("Administrator", "admin1", "admin1@sunrise-demo.test", "Admin@12345"),
    ("Teacher", "teacher1", "teacher1@sunrise-demo.test", "Teacher@12345"),
    ("Student", "student1", "student1@sunrise-demo.test", "Student@12345"),
    ("Parent", "parent1", "parent1@sunrise-demo.test", "Parent@12345"),
};

var existing = await db.Institutions.FirstOrDefaultAsync(i => i.RegistrationNumber == DemoRegistrationNumber);
if (existing is not null)
{
    Console.WriteLine($"Demo data already present (institution '{existing.Name}', id {existing.Id}).");
    PrintCredentials(existing.Name);
    return;
}

var institution = new Institution
{
    Id = Guid.NewGuid(),
    Name = "Sunrise Public School",
    RegistrationNumber = DemoRegistrationNumber,
    ContactEmail = "office@sunrise-demo.test",
    Telephone = "+911234567890",
    PostalAddress = "1 Sunrise Lane",
    PostalCode = "560001",
    City = "Bengaluru",
    Region = "Karnataka",
    // Bespoke plan so every gated capability (ParentAccess, AdvancedAnalytics, CustomReporting)
    // is unlocked for testing, instead of hitting tier-gate errors on a fresh Entry-plan tenant.
    SubscriptionPlanId = SchoolSyncDbContext.BespokePlanId,
    CreatedAtUtc = DateTime.UtcNow,
    IsActive = true,
};
db.Institutions.Add(institution);
await db.SaveChangesAsync();

var schoolClass = new Class
{
    Id = Guid.NewGuid(),
    InstitutionId = institution.Id,
    Name = "Grade 5",
    Section = "A",
};
var teacher = new Teacher
{
    Id = Guid.NewGuid(),
    InstitutionId = institution.Id,
    Name = "Anita Sharma",
    ContactEmail = "teacher1.profile@sunrise-demo.test",
    Telephone = "+911111111111",
    Gender = "Female",
    Address = "12 Teacher Street, Bengaluru",
    JoiningDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-2)),
    IsActive = true,
};
db.Classes.Add(schoolClass);
db.Teachers.Add(teacher);
await db.SaveChangesAsync();

var student = new Student
{
    Id = Guid.NewGuid(),
    InstitutionId = institution.Id,
    ClassId = schoolClass.Id,
    SrNumber = "SR-0001",
    RollNumber = "1",
    Name = "Riya Verma",
    DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-10)),
    Gender = "Female",
    Email = "student1.profile@sunrise-demo.test",
    GuardianFatherName = "Rohit Verma",
    GuardianMotherName = "Sunita Verma",
    GuardianContactEmail = "parent1.profile@sunrise-demo.test",
    GuardianPhone = "+912222222222",
    IsActive = true,
};
db.Students.Add(student);
await db.SaveChangesAsync();

var adminUser = new UserAccount
{
    Id = Guid.NewGuid(),
    InstitutionId = institution.Id,
    Username = "admin1",
    Email = "admin1@sunrise-demo.test",
    PasswordHash = hasher.Hash("Admin@12345"),
    Role = UserRole.Administrator,
    IsActive = true,
};
var teacherUser = new UserAccount
{
    Id = Guid.NewGuid(),
    InstitutionId = institution.Id,
    Username = "teacher1",
    Email = "teacher1@sunrise-demo.test",
    PasswordHash = hasher.Hash("Teacher@12345"),
    Role = UserRole.Teacher,
    TeacherId = teacher.Id,
    IsActive = true,
};
var studentUser = new UserAccount
{
    Id = Guid.NewGuid(),
    InstitutionId = institution.Id,
    Username = "student1",
    Email = "student1@sunrise-demo.test",
    PasswordHash = hasher.Hash("Student@12345"),
    Role = UserRole.Student,
    StudentId = student.Id,
    IsActive = true,
};
var parentUser = new UserAccount
{
    Id = Guid.NewGuid(),
    InstitutionId = institution.Id,
    Username = "parent1",
    Email = "parent1@sunrise-demo.test",
    PasswordHash = hasher.Hash("Parent@12345"),
    Role = UserRole.Parent,
    IsActive = true,
};
db.UserAccounts.AddRange(adminUser, teacherUser, studentUser, parentUser);
await db.SaveChangesAsync();

db.ParentStudentLinks.Add(new ParentStudentLink
{
    ParentUserId = parentUser.Id,
    StudentId = student.Id,
    CreatedAtUtc = DateTime.UtcNow,
});
await db.SaveChangesAsync();

Console.WriteLine($"Seeded institution '{institution.Name}' (id {institution.Id}), class 'Grade 5 - A', 1 teacher, 1 student, 1 parent.");
PrintCredentials(institution.Name);

void PrintCredentials(string institutionName)
{
    Console.WriteLine();
    Console.WriteLine($"Institution: {institutionName}");
    Console.WriteLine("Login with any of the following at the app's login page:");
    Console.WriteLine();
    Console.WriteLine($"{"Role",-14}{"Username",-14}{"Password",-16}Email");
    foreach (var (role, username, email, password) in credentials)
        Console.WriteLine($"{role,-14}{username,-14}{password,-16}{email}");
}

// Bypasses the tenant query filter entirely — a seeder isn't running inside any signed-in user's
// request, and every row here is written with its InstitutionId set explicitly anyway (the filter
// only affects reads, never inserts).
class SeedCurrentTenant : ICurrentTenant
{
    public Guid? InstitutionId => null;
    public bool BypassTenantFilter => true;
}
