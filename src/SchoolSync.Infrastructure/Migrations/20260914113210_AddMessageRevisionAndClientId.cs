using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolSync.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageRevisionAndClientId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Messages_RoomId",
                table: "Messages");

            migrationBuilder.AddColumn<Guid>(
                name: "ClientMessageId",
                table: "Messages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RevisedAtUtc",
                table: "Messages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WithdrawnAtUtc",
                table: "Messages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Messages_RoomId_AuthorId_ClientMessageId",
                table: "Messages",
                columns: new[] { "RoomId", "AuthorId", "ClientMessageId" },
                unique: true,
                filter: "[ClientMessageId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_RoomId_SentAtUtc",
                table: "Messages",
                columns: new[] { "RoomId", "SentAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Messages_RoomId_AuthorId_ClientMessageId",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_RoomId_SentAtUtc",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ClientMessageId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "RevisedAtUtc",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "WithdrawnAtUtc",
                table: "Messages");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_RoomId",
                table: "Messages",
                column: "RoomId");
        }
    }
}
