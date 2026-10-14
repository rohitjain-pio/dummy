using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolSync.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MeetingManyPerRoom : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Meetings_RoomId",
                table: "Meetings");

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_RoomId",
                table: "Meetings",
                column: "RoomId",
                unique: true,
                filter: "[EndedAtUtc] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Meetings_RoomId",
                table: "Meetings");

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_RoomId",
                table: "Meetings",
                column: "RoomId",
                unique: true);
        }
    }
}
