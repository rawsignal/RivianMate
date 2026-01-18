using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RivianMate.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChargingSessionDriveMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DriveMode",
                table: "ChargingSessions",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DriveMode",
                table: "ChargingSessions");
        }
    }
}
