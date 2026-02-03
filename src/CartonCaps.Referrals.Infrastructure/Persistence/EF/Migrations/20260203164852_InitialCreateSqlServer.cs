using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CartonCaps.Referrals.Infrastructure.Persistence.Ef.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreateSqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Referrals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferrerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferrerReferralCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ContactType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ContactValue = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    LinkToken = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Referrals", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_LinkToken",
                table: "Referrals",
                column: "LinkToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_ReferrerUserId_CreatedAt",
                table: "Referrals",
                columns: new[] { "ReferrerUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_ReferrerUserId_Status",
                table: "Referrals",
                columns: new[] { "ReferrerUserId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Referrals");
        }
    }
}
