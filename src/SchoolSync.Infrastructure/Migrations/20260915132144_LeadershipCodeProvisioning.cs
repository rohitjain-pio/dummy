using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolSync.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LeadershipCodeProvisioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProvisionedByCodeId",
                table: "UserAccounts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LeadershipCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CodeHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CodeLookup = table.Column<string>(type: "char(43)", nullable: false),
                    GrantedRole = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IssuedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConsumedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConsumedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadershipCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeadershipCodes_Institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "Institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserAccounts_ProvisionedByCodeId",
                table: "UserAccounts",
                column: "ProvisionedByCodeId",
                filter: "[ProvisionedByCodeId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LeadershipCodes_CodeLookup",
                table: "LeadershipCodes",
                column: "CodeLookup",
                unique: true,
                filter: "[Status] = 'Issued'");

            migrationBuilder.CreateIndex(
                name: "IX_LeadershipCodes_InstitutionId",
                table: "LeadershipCodes",
                column: "InstitutionId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserAccounts_LeadershipCodes_ProvisionedByCodeId",
                table: "UserAccounts",
                column: "ProvisionedByCodeId",
                principalTable: "LeadershipCodes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserAccounts_LeadershipCodes_ProvisionedByCodeId",
                table: "UserAccounts");

            migrationBuilder.DropTable(
                name: "LeadershipCodes");

            migrationBuilder.DropIndex(
                name: "IX_UserAccounts_ProvisionedByCodeId",
                table: "UserAccounts");

            migrationBuilder.DropColumn(
                name: "ProvisionedByCodeId",
                table: "UserAccounts");
        }
    }
}
