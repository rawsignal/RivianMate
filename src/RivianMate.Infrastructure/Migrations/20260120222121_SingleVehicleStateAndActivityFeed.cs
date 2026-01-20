using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RivianMate.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SingleVehicleStateAndActivityFeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VehicleStates_VehicleId_Timestamp",
                table: "VehicleStates");

            // Delete all but the most recent VehicleState record per vehicle
            // This is required before creating the unique index on VehicleId
            migrationBuilder.Sql(@"
                DELETE FROM ""VehicleStates""
                WHERE ""Id"" NOT IN (
                    SELECT DISTINCT ON (""VehicleId"") ""Id""
                    FROM ""VehicleStates""
                    ORDER BY ""VehicleId"", ""Timestamp"" DESC
                )
            ");

            migrationBuilder.CreateTable(
                name: "ActivityFeed",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VehicleId = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityFeed", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivityFeed_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleStates_VehicleId",
                table: "VehicleStates",
                column: "VehicleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActivityFeed_Timestamp",
                table: "ActivityFeed",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityFeed_VehicleId_Timestamp",
                table: "ActivityFeed",
                columns: new[] { "VehicleId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityFeed_VehicleId_Type_Timestamp",
                table: "ActivityFeed",
                columns: new[] { "VehicleId", "Type", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityFeed");

            migrationBuilder.DropIndex(
                name: "IX_VehicleStates_VehicleId",
                table: "VehicleStates");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleStates_VehicleId_Timestamp",
                table: "VehicleStates",
                columns: new[] { "VehicleId", "Timestamp" });
        }
    }
}
