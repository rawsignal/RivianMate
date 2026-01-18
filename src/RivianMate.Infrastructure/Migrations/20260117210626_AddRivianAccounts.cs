using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RivianMate.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRivianAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RivianAccountId",
                table: "Vehicles",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RivianAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RivianEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    RivianUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EncryptedCsrfToken = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    EncryptedAppSessionToken = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    EncryptedUserSessionToken = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    EncryptedAccessToken = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    EncryptedRefreshToken = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    TokenExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSyncAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSyncError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RivianAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RivianAccounts_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_RivianAccountId",
                table: "Vehicles",
                column: "RivianAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_RivianAccounts_UserId",
                table: "RivianAccounts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RivianAccounts_UserId_RivianEmail",
                table: "RivianAccounts",
                columns: new[] { "UserId", "RivianEmail" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Vehicles_RivianAccounts_RivianAccountId",
                table: "Vehicles",
                column: "RivianAccountId",
                principalTable: "RivianAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Vehicles_RivianAccounts_RivianAccountId",
                table: "Vehicles");

            migrationBuilder.DropTable(
                name: "RivianAccounts");

            migrationBuilder.DropIndex(
                name: "IX_Vehicles_RivianAccountId",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "RivianAccountId",
                table: "Vehicles");
        }
    }
}
