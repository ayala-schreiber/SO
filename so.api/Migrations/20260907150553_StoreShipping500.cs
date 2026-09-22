using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace so.api.Migrations
{
    /// <inheritdoc />
    public partial class StoreShipping500 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "StoreSettings",
                keyColumn: "Id",
                keyValue: 1,
                column: "FreeDeliveryAbove",
                value: 500m);
            migrationBuilder.Sql("UPDATE StoreSettings SET Version=Version+1 WHERE Id=1");
            migrationBuilder.Sql("UPDATE Products SET Color=N'תכלת', Version=Version+1 WHERE GroupKey='silk-collection' AND Color=N'כסף'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "StoreSettings",
                keyColumn: "Id",
                keyValue: 1,
                column: "FreeDeliveryAbove",
                value: 350m);
        }
    }
}
