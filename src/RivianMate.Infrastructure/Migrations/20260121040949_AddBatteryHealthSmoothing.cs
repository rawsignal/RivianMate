using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RivianMate.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBatteryHealthSmoothing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "ReadingConfidence",
                table: "BatteryHealthSnapshots",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SmoothedCapacityKwh",
                table: "BatteryHealthSnapshots",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SmoothedHealthPercent",
                table: "BatteryHealthSnapshots",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReadingConfidence",
                table: "BatteryHealthSnapshots");

            migrationBuilder.DropColumn(
                name: "SmoothedCapacityKwh",
                table: "BatteryHealthSnapshots");

            migrationBuilder.DropColumn(
                name: "SmoothedHealthPercent",
                table: "BatteryHealthSnapshots");
        }
    }
}
