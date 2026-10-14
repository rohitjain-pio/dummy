using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolSync.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TimetablePublishState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPublished",
                table: "TimetablePeriods",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAtUtc",
                table: "TimetablePeriods",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPublished",
                table: "TimetablePeriods");

            migrationBuilder.DropColumn(
                name: "PublishedAtUtc",
                table: "TimetablePeriods");
        }
    }
}
