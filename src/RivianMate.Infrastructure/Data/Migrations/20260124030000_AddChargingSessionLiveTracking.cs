using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RivianMate.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChargingSessionLiveTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "CurrentBatteryLevel",
                table: "ChargingSessions",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CurrentRangeEstimate",
                table: "ChargingSessions",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastUpdatedAt",
                table: "ChargingSessions",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentBatteryLevel",
                table: "ChargingSessions");

            migrationBuilder.DropColumn(
                name: "CurrentRangeEstimate",
                table: "ChargingSessions");

            migrationBuilder.DropColumn(
                name: "LastUpdatedAt",
                table: "ChargingSessions");
        }
    }
}
