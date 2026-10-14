using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SchoolSync.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BeforeSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AfterSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TimestampUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IdempotencyRecords",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ResponseBodyJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResponseStatusCode = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyRecords", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "PublicInstitutionDirectory",
                columns: table => new
                {
                    InstitutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    City = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Region = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicInstitutionDirectory", x => x.InstitutionId);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LearnerCapacity = table.Column<int>(type: "int", nullable: false),
                    StorageAllowanceGb = table.Column<int>(type: "int", nullable: false),
                    SupportLevel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CapabilitiesJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Institutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RegistrationNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContactEmail = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Telephone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PostalAddress = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PostalCode = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    City = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Region = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeactivatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubscriptionPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Institutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Institutions_SubscriptionPlans_SubscriptionPlanId",
                        column: x => x.SubscriptionPlanId,
                        principalTable: "SubscriptionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AcademicYears",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcademicYears", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AcademicYears_Institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "Institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PersonType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    AttendanceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    RecordedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CorrectionStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceRecords_Institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "Institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InstitutionConfigurations",
                columns: table => new
                {
                    InstitutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcademicYear = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TimeZone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GradingScale = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CourseCategories = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AssessmentTypes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReportCardFormat = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NotificationPreferencesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstitutionConfigurations", x => x.InstitutionId);
                    table.ForeignKey(
                        name: "FK_InstitutionConfigurations_Institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "Institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReportRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GeneratedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GeneratedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BlobUri = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportRuns_Institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "Institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Subjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subjects_Institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "Institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Teachers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContactEmail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Telephone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Gender = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    JoiningDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeactivatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teachers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Teachers_Institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "Institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Terms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Terms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Terms_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Classes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Section = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClassTeacherId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Classes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Classes_Institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "Institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Classes_Teachers_ClassTeacherId",
                        column: x => x.ClassTeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Students",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SrNumber = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RollNumber = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: false),
                    Gender = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ClassId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GuardianFatherName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GuardianMotherName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GuardianContactEmail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GuardianPhone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeactivatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Students", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Students_Classes_ClassId",
                        column: x => x.ClassId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Students_Institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "Institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubjectTeacherClassMappings",
                columns: table => new
                {
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClassId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubjectTeacherClassMappings", x => new { x.SubjectId, x.TeacherId, x.ClassId });
                    table.ForeignKey(
                        name: "FK_SubjectTeacherClassMappings_Classes_ClassId",
                        column: x => x.ClassId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubjectTeacherClassMappings_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubjectTeacherClassMappings_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TimetablePeriods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClassId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Room = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DayOfWeek = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TimeSlot = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimetablePeriods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TimetablePeriods_Classes_ClassId",
                        column: x => x.ClassId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TimetablePeriods_Institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "Institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TimetablePeriods_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TimetablePeriods_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Enrollments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcademicYearId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClassId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Enrollments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Enrollments_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Enrollments_Classes_ClassId",
                        column: x => x.ClassId,
                        principalTable: "Classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Enrollments_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ParentLinkCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IssuedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConsumedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConsumedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParentLinkCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParentLinkCodes_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TeacherId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeactivatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserAccounts_Institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "Institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserAccounts_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserAccounts_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Announcements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsWithdrawn = table.Column<bool>(type: "bit", nullable: false),
                    WithdrawnAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Announcements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Announcements_Institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "Institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Announcements_UserAccounts_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ParentStudentLinks",
                columns: table => new
                {
                    ParentUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParentStudentLinks", x => new { x.ParentUserId, x.StudentId });
                    table.ForeignKey(
                        name: "FK_ParentStudentLinks_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParentStudentLinks_UserAccounts_ParentUserId",
                        column: x => x.ParentUserId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RotatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_UserAccounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Rooms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PasscodeHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeactivatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rooms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rooms_Institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "Institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Rooms_UserAccounts_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Meetings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Meetings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Meetings_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsWithdrawn = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Messages_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Messages_UserAccounts_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "SubscriptionPlans",
                columns: new[] { "Id", "CapabilitiesJson", "LearnerCapacity", "Name", "StorageAllowanceGb", "SupportLevel" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000001"), "[]", 200, "Entry", 5, "Standard" },
                    { new Guid("00000000-0000-0000-0000-000000000002"), "[\"AdvancedAnalytics\"]", 1000, "Mid", 25, "Priority" },
                    { new Guid("00000000-0000-0000-0000-000000000003"), "[\"AdvancedAnalytics\",\"ParentAccess\",\"CustomReporting\"]", 2147483647, "Bespoke", 500, "Dedicated" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AcademicYears_InstitutionId",
                table: "AcademicYears",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_AuthorId",
                table: "Announcements",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_InstitutionId",
                table: "Announcements",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_InstitutionId_AttendanceDate",
                table: "AttendanceRecords",
                columns: new[] { "InstitutionId", "AttendanceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_PersonId_AttendanceDate",
                table: "AttendanceRecords",
                columns: new[] { "PersonId", "AttendanceDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_InstitutionId",
                table: "AuditLogs",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TimestampUtc",
                table: "AuditLogs",
                column: "TimestampUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Classes_ClassTeacherId",
                table: "Classes",
                column: "ClassTeacherId",
                unique: true,
                filter: "[ClassTeacherId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Classes_InstitutionId_Name_Section",
                table: "Classes",
                columns: new[] { "InstitutionId", "Name", "Section" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_AcademicYearId",
                table: "Enrollments",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_ClassId",
                table: "Enrollments",
                column: "ClassId");

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_StudentId",
                table: "Enrollments",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_Institutions_City_Region",
                table: "Institutions",
                columns: new[] { "City", "Region" });

            migrationBuilder.CreateIndex(
                name: "IX_Institutions_Name_ContactEmail_PostalCode",
                table: "Institutions",
                columns: new[] { "Name", "ContactEmail", "PostalCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Institutions_SubscriptionPlanId",
                table: "Institutions",
                column: "SubscriptionPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_RoomId",
                table: "Meetings",
                column: "RoomId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Messages_AuthorId",
                table: "Messages",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_RoomId",
                table: "Messages",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_ParentLinkCodes_Code",
                table: "ParentLinkCodes",
                column: "Code",
                unique: true,
                filter: "[ConsumedAtUtc] IS NULL AND [RevokedAtUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ParentLinkCodes_StudentId",
                table: "ParentLinkCodes",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_ParentStudentLinks_StudentId",
                table: "ParentStudentLinks",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_PublicInstitutionDirectory_City_Region",
                table: "PublicInstitutionDirectory",
                columns: new[] { "City", "Region" });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TokenHash",
                table: "RefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserAccountId",
                table: "RefreshTokens",
                column: "UserAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportRuns_InstitutionId",
                table: "ReportRuns",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_CreatedByUserId",
                table: "Rooms",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_InstitutionId",
                table: "Rooms",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_Students_ClassId",
                table: "Students",
                column: "ClassId");

            migrationBuilder.CreateIndex(
                name: "IX_Students_InstitutionId_ClassId",
                table: "Students",
                columns: new[] { "InstitutionId", "ClassId" });

            migrationBuilder.CreateIndex(
                name: "IX_Students_InstitutionId_RollNumber",
                table: "Students",
                columns: new[] { "InstitutionId", "RollNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Students_InstitutionId_SrNumber",
                table: "Students",
                columns: new[] { "InstitutionId", "SrNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_InstitutionId",
                table: "Subjects",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_SubjectTeacherClassMappings_ClassId",
                table: "SubjectTeacherClassMappings",
                column: "ClassId");

            migrationBuilder.CreateIndex(
                name: "IX_SubjectTeacherClassMappings_TeacherId",
                table: "SubjectTeacherClassMappings",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_Teachers_InstitutionId",
                table: "Teachers",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_Terms_AcademicYearId",
                table: "Terms",
                column: "AcademicYearId");

            migrationBuilder.CreateIndex(
                name: "IX_TimetablePeriods_ClassId_DayOfWeek_TimeSlot",
                table: "TimetablePeriods",
                columns: new[] { "ClassId", "DayOfWeek", "TimeSlot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TimetablePeriods_InstitutionId",
                table: "TimetablePeriods",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_TimetablePeriods_Room_InstitutionId_DayOfWeek_TimeSlot",
                table: "TimetablePeriods",
                columns: new[] { "Room", "InstitutionId", "DayOfWeek", "TimeSlot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TimetablePeriods_SubjectId",
                table: "TimetablePeriods",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_TimetablePeriods_TeacherId_DayOfWeek_TimeSlot",
                table: "TimetablePeriods",
                columns: new[] { "TeacherId", "DayOfWeek", "TimeSlot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserAccounts_Email",
                table: "UserAccounts",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserAccounts_InstitutionId",
                table: "UserAccounts",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAccounts_StudentId",
                table: "UserAccounts",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAccounts_TeacherId",
                table: "UserAccounts",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAccounts_Username",
                table: "UserAccounts",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Announcements");

            migrationBuilder.DropTable(
                name: "AttendanceRecords");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "Enrollments");

            migrationBuilder.DropTable(
                name: "IdempotencyRecords");

            migrationBuilder.DropTable(
                name: "InstitutionConfigurations");

            migrationBuilder.DropTable(
                name: "Meetings");

            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropTable(
                name: "ParentLinkCodes");

            migrationBuilder.DropTable(
                name: "ParentStudentLinks");

            migrationBuilder.DropTable(
                name: "PublicInstitutionDirectory");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "ReportRuns");

            migrationBuilder.DropTable(
                name: "SubjectTeacherClassMappings");

            migrationBuilder.DropTable(
                name: "Terms");

            migrationBuilder.DropTable(
                name: "TimetablePeriods");

            migrationBuilder.DropTable(
                name: "Rooms");

            migrationBuilder.DropTable(
                name: "AcademicYears");

            migrationBuilder.DropTable(
                name: "Subjects");

            migrationBuilder.DropTable(
                name: "UserAccounts");

            migrationBuilder.DropTable(
                name: "Students");

            migrationBuilder.DropTable(
                name: "Classes");

            migrationBuilder.DropTable(
                name: "Teachers");

            migrationBuilder.DropTable(
                name: "Institutions");

            migrationBuilder.DropTable(
                name: "SubscriptionPlans");
        }
    }
}
