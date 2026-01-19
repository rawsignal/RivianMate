using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RivianMate.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGearTunnelClosures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SideBinLeftClosed",
                table: "VehicleStates",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SideBinLeftLocked",
                table: "VehicleStates",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SideBinRightClosed",
                table: "VehicleStates",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SideBinRightLocked",
                table: "VehicleStates",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SideBinLeftClosed",
                table: "VehicleStates");

            migrationBuilder.DropColumn(
                name: "SideBinLeftLocked",
                table: "VehicleStates");

            migrationBuilder.DropColumn(
                name: "SideBinRightClosed",
                table: "VehicleStates");

            migrationBuilder.DropColumn(
                name: "SideBinRightLocked",
                table: "VehicleStates");
        }
    }
}
