using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RivianMate.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChangeGearGuardToStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GearGuardEnabled",
                table: "VehicleStates");

            migrationBuilder.AddColumn<string>(
                name: "GearGuardStatus",
                table: "VehicleStates",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GearGuardStatus",
                table: "VehicleStates");

            migrationBuilder.AddColumn<bool>(
                name: "GearGuardEnabled",
                table: "VehicleStates",
                type: "boolean",
                nullable: true);
        }
    }
}
