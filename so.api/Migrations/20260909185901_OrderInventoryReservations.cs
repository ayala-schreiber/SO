using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace so.api.Migrations
{
    /// <inheritdoc />
    public partial class OrderInventoryReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReservationMinutes",
                table: "StoreSettings",
                type: "int",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReservationExpiresAt",
                table: "ShopOrders",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "StoreSettings",
                keyColumn: "Id",
                keyValue: 1,
                column: "ReservationMinutes",
                value: 60);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReservationMinutes",
                table: "StoreSettings");

            migrationBuilder.DropColumn(
                name: "ReservationExpiresAt",
                table: "ShopOrders");
        }
    }
}
