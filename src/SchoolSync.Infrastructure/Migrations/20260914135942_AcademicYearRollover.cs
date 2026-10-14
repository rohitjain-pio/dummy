using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolSync.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AcademicYearRollover : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Enrollments_AcademicYearId",
                table: "Enrollments");

            migrationBuilder.AddColumn<Guid>(
                name: "NextClassId",
                table: "Classes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "NextAcademicYearId",
                table: "AcademicYears",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RolledOverAtUtc",
                table: "AcademicYears",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RolloverRequestedAtUtc",
                table: "AcademicYears",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RolloverHoldBacks",
                columns: table => new
                {
                    AcademicYearId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolloverHoldBacks", x => new { x.AcademicYearId, x.StudentId });
                    table.ForeignKey(
                        name: "FK_RolloverHoldBacks_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolloverHoldBacks_Students_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Students",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_AcademicYearId_StudentId_CreatedAtUtc",
                table: "Enrollments",
                columns: new[] { "AcademicYearId", "StudentId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Classes_NextClassId",
                table: "Classes",
                column: "NextClassId");

            migrationBuilder.CreateIndex(
                name: "IX_RolloverHoldBacks_StudentId",
                table: "RolloverHoldBacks",
                column: "StudentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Classes_Classes_NextClassId",
                table: "Classes",
                column: "NextClassId",
                principalTable: "Classes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Classes_Classes_NextClassId",
                table: "Classes");

            migrationBuilder.DropTable(
                name: "RolloverHoldBacks");

            migrationBuilder.DropIndex(
                name: "IX_Enrollments_AcademicYearId_StudentId_CreatedAtUtc",
                table: "Enrollments");

            migrationBuilder.DropIndex(
                name: "IX_Classes_NextClassId",
                table: "Classes");

            migrationBuilder.DropColumn(
                name: "NextClassId",
                table: "Classes");

            migrationBuilder.DropColumn(
                name: "NextAcademicYearId",
                table: "AcademicYears");

            migrationBuilder.DropColumn(
                name: "RolledOverAtUtc",
                table: "AcademicYears");

            migrationBuilder.DropColumn(
                name: "RolloverRequestedAtUtc",
                table: "AcademicYears");

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_AcademicYearId",
                table: "Enrollments",
                column: "AcademicYearId");
        }
    }
}
