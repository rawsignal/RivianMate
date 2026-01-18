using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RivianMate.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "ImageData",
                table: "Vehicles",
                type: "BLOB",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageContentType",
                table: "Vehicles",
                type: "TEXT",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageData",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "ImageContentType",
                table: "Vehicles");
        }
    }
}
