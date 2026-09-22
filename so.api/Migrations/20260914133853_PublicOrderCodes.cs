using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace so.api.Migrations
{
    /// <inheritdoc />
    public partial class PublicOrderCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LegacyNumber",
                table: "ShopOrders",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublicCode",
                table: "ShopOrders",
                type: "nvarchar(11)",
                maxLength: 11,
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "StoreSettings",
                keyColumn: "Id",
                keyValue: 1,
                column: "ReservationMinutes",
                value: 30);

            migrationBuilder.Sql("\nUPDATE [ShopOrders] SET [LegacyNumber]='SO-'+CASE WHEN [Id]<1000000 THEN RIGHT('000000'+CAST([Id] AS varchar(20)),6) ELSE CAST([Id] AS varchar(20)) END WHERE [PublicCode]='';\nDECLARE @id int, @code nvarchar(11), @bytes varbinary(8), @i int;\nDECLARE codes CURSOR LOCAL FAST_FORWARD FOR SELECT [Id] FROM [ShopOrders] WHERE [PublicCode]='';\nOPEN codes; FETCH NEXT FROM codes INTO @id;\nWHILE @@FETCH_STATUS=0\nBEGIN\n SET @code='';\n WHILE @code='' OR EXISTS(SELECT 1 FROM [ShopOrders] WHERE [PublicCode]=@code)\n BEGIN\n  SET @bytes=CRYPT_GEN_RANDOM(8); SET @code='SO-'; SET @i=1;\n  WHILE @i<=8 BEGIN SET @code=@code+SUBSTRING('ABCDEFGHJKLMNPQRSTUVWXYZ23456789',CONVERT(int,SUBSTRING(@bytes,@i,1))%32+1,1); SET @i=@i+1; END;\n END;\n UPDATE [ShopOrders] SET [PublicCode]=@code WHERE [Id]=@id;\n FETCH NEXT FROM codes INTO @id;\nEND;\nCLOSE codes; DEALLOCATE codes;\nUPDATE [StoreSettings] SET [Version]=[Version]+1 WHERE [Id]=1;\n");

            migrationBuilder.CreateIndex(
                name: "IX_ShopOrders_PublicCode",
                table: "ShopOrders",
                column: "PublicCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ShopOrders_PublicCode",
                table: "ShopOrders");

            migrationBuilder.DropColumn(
                name: "LegacyNumber",
                table: "ShopOrders");

            migrationBuilder.DropColumn(
                name: "PublicCode",
                table: "ShopOrders");

            migrationBuilder.UpdateData(
                table: "StoreSettings",
                keyColumn: "Id",
                keyValue: 1,
                column: "ReservationMinutes",
                value: 60);
        }
    }
}
