using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace so.api.Migrations
{
    /// <inheritdoc />
    public partial class EmailVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EmailVerified",
                table: "Customers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "VerificationExpiresAt",
                table: "Customers",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerificationHash",
                table: "Customers",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmailVerified",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "VerificationExpiresAt",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "VerificationHash",
                table: "Customers");
        }
    }
}
