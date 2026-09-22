using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace so.api.Migrations
{
    /// <inheritdoc />
    public partial class ManualPaymentApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PaidAt",
                table: "ShopOrders",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentConfirmedBy",
                table: "ShopOrders",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "ShopOrders",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentRecipient",
                table: "ShopOrders",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentRecipientName",
                table: "ShopOrders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentReference",
                table: "ShopOrders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ManualPaymentSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PayPalRecipient = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    PayPalName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BitPhone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    BitName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManualPaymentSettings", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "ManualPaymentSettings",
                columns: new[] { "Id", "BitName", "BitPhone", "PayPalName", "PayPalRecipient", "Version" },
                values: new object[] { 1, "חנות הדגמה", "0000000000", "", "", 0 });

            migrationBuilder.CreateIndex(
                name: "IX_ShopOrders_PaymentMethod_PaymentReference",
                table: "ShopOrders",
                columns: new[] { "PaymentMethod", "PaymentReference" },
                unique: true,
                filter: "[PaymentReference] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ManualPaymentSettings");

            migrationBuilder.DropIndex(
                name: "IX_ShopOrders_PaymentMethod_PaymentReference",
                table: "ShopOrders");

            migrationBuilder.DropColumn(
                name: "PaidAt",
                table: "ShopOrders");

            migrationBuilder.DropColumn(
                name: "PaymentConfirmedBy",
                table: "ShopOrders");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "ShopOrders");

            migrationBuilder.DropColumn(
                name: "PaymentRecipient",
                table: "ShopOrders");

            migrationBuilder.DropColumn(
                name: "PaymentRecipientName",
                table: "ShopOrders");

            migrationBuilder.DropColumn(
                name: "PaymentReference",
                table: "ShopOrders");
        }
    }
}
