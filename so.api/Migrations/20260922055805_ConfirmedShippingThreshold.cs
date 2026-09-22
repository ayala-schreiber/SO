using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace so.api.Migrations;

public partial class ConfirmedShippingThreshold : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Replace the superseded threshold only; preserve other owner settings.
        migrationBuilder.Sql("UPDATE [StoreSettings] SET [FreeDeliveryAbove] = 500, [Version] = [Version] + 1 WHERE [Id] = 1 AND [FreeDeliveryAbove] = 399;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // A code rollback must not revert the owner's current shipping policy.
    }
}
