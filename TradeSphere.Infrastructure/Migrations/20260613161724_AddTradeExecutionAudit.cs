using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradeSphere.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTradeExecutionAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BrokerResponse",
                table: "Trades",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ErrorReason",
                table: "Trades",
                type: "text",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Exchanges",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 6, 13, 16, 17, 24, 51, DateTimeKind.Utc).AddTicks(4056));

            migrationBuilder.UpdateData(
                table: "Exchanges",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 6, 13, 16, 17, 24, 51, DateTimeKind.Utc).AddTicks(4059));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BrokerResponse",
                table: "Trades");

            migrationBuilder.DropColumn(
                name: "ErrorReason",
                table: "Trades");

            migrationBuilder.UpdateData(
                table: "Exchanges",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 6, 13, 14, 56, 44, 923, DateTimeKind.Utc).AddTicks(3725));

            migrationBuilder.UpdateData(
                table: "Exchanges",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 6, 13, 14, 56, 44, 923, DateTimeKind.Utc).AddTicks(3729));
        }
    }
}
