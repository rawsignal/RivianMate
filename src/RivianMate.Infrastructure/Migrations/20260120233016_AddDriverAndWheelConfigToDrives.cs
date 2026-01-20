using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RivianMate.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverAndWheelConfigToDrives : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DriverName",
                table: "Drives",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WheelConfig",
                table: "Drives",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DriverName",
                table: "Drives");

            migrationBuilder.DropColumn(
                name: "WheelConfig",
                table: "Drives");
        }
    }
}
