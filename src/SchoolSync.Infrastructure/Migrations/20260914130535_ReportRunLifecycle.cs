using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolSync.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReportRunLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReportRuns_InstitutionId",
                table: "ReportRuns");

            migrationBuilder.AlterColumn<string>(
                name: "ReportType",
                table: "ReportRuns",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "GeneratedAtUtc",
                table: "ReportRuns",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<string>(
                name: "BlobUri",
                table: "ReportRuns",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                table: "ReportRuns",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Format",
                table: "ReportRuns",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateOnly>(
                name: "FromDate",
                table: "ReportRuns",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<DateTime>(
                name: "RequestedAtUtc",
                table: "ReportRuns",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "ScopeClassId",
                table: "ReportRuns",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ScopeTeacherUserId",
                table: "ReportRuns",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "ReportRuns",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateOnly>(
                name: "ToDate",
                table: "ReportRuns",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.CreateIndex(
                name: "IX_ReportRuns_InstitutionId_RequestedAtUtc",
                table: "ReportRuns",
                columns: new[] { "InstitutionId", "RequestedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReportRuns_InstitutionId_RequestedAtUtc",
                table: "ReportRuns");

            migrationBuilder.DropColumn(
                name: "FailureReason",
                table: "ReportRuns");

            migrationBuilder.DropColumn(
                name: "Format",
                table: "ReportRuns");

            migrationBuilder.DropColumn(
                name: "FromDate",
                table: "ReportRuns");

            migrationBuilder.DropColumn(
                name: "RequestedAtUtc",
                table: "ReportRuns");

            migrationBuilder.DropColumn(
                name: "ScopeClassId",
                table: "ReportRuns");

            migrationBuilder.DropColumn(
                name: "ScopeTeacherUserId",
                table: "ReportRuns");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ReportRuns");

            migrationBuilder.DropColumn(
                name: "ToDate",
                table: "ReportRuns");

            migrationBuilder.AlterColumn<string>(
                name: "ReportType",
                table: "ReportRuns",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<DateTime>(
                name: "GeneratedAtUtc",
                table: "ReportRuns",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BlobUri",
                table: "ReportRuns",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportRuns_InstitutionId",
                table: "ReportRuns",
                column: "InstitutionId");
        }
    }
}
