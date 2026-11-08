using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MultiCarrierApi.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAddressPhone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ShipFrom_Phone",
                table: "Shipments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShipTo_Phone",
                table: "Shipments",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShipFrom_Phone",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "ShipTo_Phone",
                table: "Shipments");
        }
    }
}
