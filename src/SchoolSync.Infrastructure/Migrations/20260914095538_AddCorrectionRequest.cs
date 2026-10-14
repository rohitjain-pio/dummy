using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolSync.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCorrectionRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CorrectionRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttendanceRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedOutcome = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ResolvedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolutionNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrectionRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorrectionRequests_AttendanceRecords_AttendanceRecordId",
                        column: x => x.AttendanceRecordId,
                        principalTable: "AttendanceRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CorrectionRequests_Institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "Institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CorrectionRequests_AttendanceRecordId",
                table: "CorrectionRequests",
                column: "AttendanceRecordId",
                unique: true,
                filter: "[Status] = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_CorrectionRequests_InstitutionId",
                table: "CorrectionRequests",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrectionRequests_Status_RequestedAtUtc",
                table: "CorrectionRequests",
                columns: new[] { "Status", "RequestedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CorrectionRequests");
        }
    }
}
