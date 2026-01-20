using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RivianMate.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveVehicleStateUniqueConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VehicleStates_VehicleId",
                table: "VehicleStates");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleStates_VehicleId",
                table: "VehicleStates",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleStates_VehicleId_Timestamp",
                table: "VehicleStates",
                columns: new[] { "VehicleId", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VehicleStates_VehicleId",
                table: "VehicleStates");

            migrationBuilder.DropIndex(
                name: "IX_VehicleStates_VehicleId_Timestamp",
                table: "VehicleStates");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleStates_VehicleId",
                table: "VehicleStates",
                column: "VehicleId",
                unique: true);
        }
    }
}
