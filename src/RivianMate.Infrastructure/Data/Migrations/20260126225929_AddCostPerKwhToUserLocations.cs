using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RivianMate.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCostPerKwhToUserLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "IsInServiceMode",
                table: "VehicleStates",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AddColumn<DateTime>(
                name: "BuildDate",
                table: "Vehicles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CostPerKwh",
                table: "UserLocations",
                type: "double precision",
                nullable: true);

            // Use IF NOT EXISTS because this column may already exist in production
            migrationBuilder.Sql(
                @"ALTER TABLE ""ChargingSessions"" ADD COLUMN IF NOT EXISTS ""UserLocationId"" integer;");

            migrationBuilder.AddColumn<string>(
                name: "ReferralCode",
                table: "AspNetUsers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReferredByUserId",
                table: "AspNetUsers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DataExports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<int>(type: "integer", nullable: false),
                    ExportType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DownloadToken = table.Column<Guid>(type: "uuid", nullable: false),
                    FileData = table.Column<byte[]>(type: "bytea", nullable: true),
                    FileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    RecordCount = table.Column<int>(type: "integer", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DownloadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataExports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DataExports_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DataExports_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PromoCampaigns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CampaignType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreditsPerReward = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    StartsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MaxRedemptionsPerUser = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromoCampaigns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Referrals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CampaignId = table.Column<int>(type: "integer", nullable: false),
                    ReferrerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferredUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferralCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    QualifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RewardedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Referrals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Referrals_AspNetUsers_ReferredUserId",
                        column: x => x.ReferredUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Referrals_AspNetUsers_ReferrerId",
                        column: x => x.ReferrerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Referrals_PromoCampaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "PromoCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PromoCredits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<int>(type: "integer", nullable: false),
                    ReferralId = table.Column<int>(type: "integer", nullable: true),
                    Credits = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConsumedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromoCredits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PromoCredits_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PromoCredits_PromoCampaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "PromoCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PromoCredits_Referrals_ReferralId",
                        column: x => x.ReferralId,
                        principalTable: "Referrals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.Sql(
                @"CREATE INDEX IF NOT EXISTS ""IX_ChargingSessions_UserLocationId"" ON ""ChargingSessions"" (""UserLocationId"");");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_ReferralCode",
                table: "AspNetUsers",
                column: "ReferralCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DataExports_DownloadToken",
                table: "DataExports",
                column: "DownloadToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DataExports_ExpiresAt",
                table: "DataExports",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_DataExports_UserId",
                table: "DataExports",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_DataExports_VehicleId",
                table: "DataExports",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_PromoCredits_CampaignId",
                table: "PromoCredits",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_PromoCredits_ReferralId",
                table: "PromoCredits",
                column: "ReferralId");

            migrationBuilder.CreateIndex(
                name: "IX_PromoCredits_UserId",
                table: "PromoCredits",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PromoCredits_UserId_ConsumedAt",
                table: "PromoCredits",
                columns: new[] { "UserId", "ConsumedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_CampaignId",
                table: "Referrals",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_ReferredUserId",
                table: "Referrals",
                column: "ReferredUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_ReferredUserId_Status",
                table: "Referrals",
                columns: new[] { "ReferredUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_ReferrerId",
                table: "Referrals",
                column: "ReferrerId");

            migrationBuilder.Sql(
                @"DO $$ BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_ChargingSessions_UserLocations_UserLocationId') THEN
                        ALTER TABLE ""ChargingSessions"" ADD CONSTRAINT ""FK_ChargingSessions_UserLocations_UserLocationId""
                            FOREIGN KEY (""UserLocationId"") REFERENCES ""UserLocations"" (""Id"") ON DELETE SET NULL;
                    END IF;
                END $$;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChargingSessions_UserLocations_UserLocationId",
                table: "ChargingSessions");

            migrationBuilder.DropTable(
                name: "DataExports");

            migrationBuilder.DropTable(
                name: "PromoCredits");

            migrationBuilder.DropTable(
                name: "Referrals");

            migrationBuilder.DropTable(
                name: "PromoCampaigns");

            migrationBuilder.DropIndex(
                name: "IX_ChargingSessions_UserLocationId",
                table: "ChargingSessions");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_ReferralCode",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "BuildDate",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "CostPerKwh",
                table: "UserLocations");

            migrationBuilder.DropColumn(
                name: "UserLocationId",
                table: "ChargingSessions");

            migrationBuilder.DropColumn(
                name: "ReferralCode",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ReferredByUserId",
                table: "AspNetUsers");

            migrationBuilder.AlterColumn<bool>(
                name: "IsInServiceMode",
                table: "VehicleStates",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);
        }
    }
}
