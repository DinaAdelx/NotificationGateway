using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NotificationGateway.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateNotificationGatewayModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "TenantProviderConfigurations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "Notifications",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UserId",
                table: "TenantProviderConfigurations");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Notifications");
        }
    }
}
