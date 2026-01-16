using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareFleet.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailOtp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "EmailVerificationToken",
                table: "Users",
                newName: "EmailOtp");

            migrationBuilder.AddColumn<DateTime>(
                name: "OtpExpiryTime",
                table: "Users",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OtpExpiryTime",
                table: "Users");

            migrationBuilder.RenameColumn(
                name: "EmailOtp",
                table: "Users",
                newName: "EmailVerificationToken");
        }
    }
}
