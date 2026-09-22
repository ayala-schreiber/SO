using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace so.api.Migrations
{
    /// <inheritdoc />
    public partial class ProductContentAndPickup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PickupWhatsAppUrl",
                table: "StoreSettings",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Bobo",
                table: "Products",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Breathability",
                table: "Products",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FabricDescription",
                table: "Products",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Opacity",
                table: "Products",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Season",
                table: "Products",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Slip",
                table: "Products",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Stretch",
                table: "Products",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuitableFor",
                table: "Products",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "StoreSettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FreeDeliveryAbove", "PickupAddress", "PickupWhatsAppUrl" },
                values: new object[] { 399m, "כתובת לדוגמה", null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PickupWhatsAppUrl",
                table: "StoreSettings");

            migrationBuilder.DropColumn(
                name: "Bobo",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Breathability",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "FabricDescription",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Opacity",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Season",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Slip",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Stretch",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "SuitableFor",
                table: "Products");

            migrationBuilder.UpdateData(
                table: "StoreSettings",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "FreeDeliveryAbove", "PickupAddress" },
                values: new object[] { 500m, "כתובת לדוגמה" });
        }
    }
}
