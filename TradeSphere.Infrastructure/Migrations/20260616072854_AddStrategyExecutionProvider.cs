using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradeSphere.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStrategyExecutionProvider : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExecutionProvider",
                table: "UserStrategies",
                type: "text",
                nullable: false,
                defaultValue: "Delta");

            migrationBuilder.AddColumn<int>(
                name: "Mt5AccountId",
                table: "UserStrategies",
                type: "integer",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Exchanges",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 6, 16, 7, 28, 54, 179, DateTimeKind.Utc).AddTicks(765));

            migrationBuilder.UpdateData(
                table: "Exchanges",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 6, 16, 7, 28, 54, 179, DateTimeKind.Utc).AddTicks(768));

            migrationBuilder.UpdateData(
                table: "Exchanges",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 6, 16, 7, 28, 54, 179, DateTimeKind.Utc).AddTicks(769));

            migrationBuilder.CreateIndex(
                name: "IX_UserStrategies_Mt5AccountId",
                table: "UserStrategies",
                column: "Mt5AccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserStrategies_Mt5Accounts_Mt5AccountId",
                table: "UserStrategies",
                column: "Mt5AccountId",
                principalTable: "Mt5Accounts",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserStrategies_Mt5Accounts_Mt5AccountId",
                table: "UserStrategies");

            migrationBuilder.DropIndex(
                name: "IX_UserStrategies_Mt5AccountId",
                table: "UserStrategies");

            migrationBuilder.DropColumn(
                name: "ExecutionProvider",
                table: "UserStrategies");

            migrationBuilder.DropColumn(
                name: "Mt5AccountId",
                table: "UserStrategies");

            migrationBuilder.UpdateData(
                table: "Exchanges",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 6, 16, 5, 56, 3, 502, DateTimeKind.Utc).AddTicks(9165));

            migrationBuilder.UpdateData(
                table: "Exchanges",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 6, 16, 5, 56, 3, 502, DateTimeKind.Utc).AddTicks(9168));

            migrationBuilder.UpdateData(
                table: "Exchanges",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 6, 16, 5, 56, 3, 502, DateTimeKind.Utc).AddTicks(9169));
        }
    }
}
