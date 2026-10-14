using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolSync.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SubscriptionAndTrialFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "SubscriptionStartDate",
                table: "Institutions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TrialExpiresAtUtc",
                table: "Institutions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TrialPlanId",
                table: "Institutions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TrialStartedAtUtc",
                table: "Institutions",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubscriptionStartDate",
                table: "Institutions");

            migrationBuilder.DropColumn(
                name: "TrialExpiresAtUtc",
                table: "Institutions");

            migrationBuilder.DropColumn(
                name: "TrialPlanId",
                table: "Institutions");

            migrationBuilder.DropColumn(
                name: "TrialStartedAtUtc",
                table: "Institutions");
        }
    }
}
