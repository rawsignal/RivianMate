using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RivianMate.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVehiclePublicId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add column without unique constraint first
            migrationBuilder.AddColumn<Guid>(
                name: "PublicId",
                table: "Vehicles",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Generate unique GUIDs for existing vehicles (PostgreSQL)
            migrationBuilder.Sql("UPDATE \"Vehicles\" SET \"PublicId\" = gen_random_uuid()");

            // Now add the unique index
            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_PublicId",
                table: "Vehicles",
                column: "PublicId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Vehicles_PublicId",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "PublicId",
                table: "Vehicles");
        }
    }
}
