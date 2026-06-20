using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradeSphere.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserExchangeIdToStrategy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UserExchangeId",
                table: "UserStrategies",
                type: "integer",
                nullable: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_UserStrategies_UserExchangeId",
                table: "UserStrategies",
                column: "UserExchangeId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserStrategies_UserExchanges_UserExchangeId",
                table: "UserStrategies",
                column: "UserExchangeId",
                principalTable: "UserExchanges",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserStrategies_UserExchanges_UserExchangeId",
                table: "UserStrategies");

            migrationBuilder.DropIndex(
                name: "IX_UserStrategies_UserExchangeId",
                table: "UserStrategies");

            migrationBuilder.DropColumn(
                name: "UserExchangeId",
                table: "UserStrategies");

            migrationBuilder.UpdateData(
                table: "Exchanges",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 6, 13, 9, 55, 25, 375, DateTimeKind.Utc).AddTicks(5206));

            migrationBuilder.UpdateData(
                table: "Exchanges",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 6, 13, 9, 55, 25, 375, DateTimeKind.Utc).AddTicks(5210));
        }
    }
}
