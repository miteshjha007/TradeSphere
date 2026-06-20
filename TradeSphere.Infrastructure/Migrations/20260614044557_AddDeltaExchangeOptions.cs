using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradeSphere.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDeltaExchangeOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Exchanges",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "Name" },
                values: new object[] { new DateTime(2026, 6, 14, 4, 45, 55, 897, DateTimeKind.Utc).AddTicks(1845), "Delta Exchange India" });

            migrationBuilder.UpdateData(
                table: "Exchanges",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "BaseUrl", "CreatedAt", "Name" },
                values: new object[] { "https://api.delta.exchange", new DateTime(2026, 6, 14, 4, 45, 55, 897, DateTimeKind.Utc).AddTicks(1849), "Delta Exchange Global" });

            migrationBuilder.InsertData(
                table: "Exchanges",
                columns: new[] { "Id", "BaseUrl", "CreatedAt", "IsActive", "Name", "UpdatedAt" },
                values: new object[] { 3, "https://testnet-api.delta.exchange", new DateTime(2026, 6, 14, 4, 45, 55, 897, DateTimeKind.Utc).AddTicks(1850), true, "Delta Exchange Testnet", null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Exchanges",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.UpdateData(
                table: "Exchanges",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "CreatedAt", "Name" },
                values: new object[] { new DateTime(2026, 6, 13, 16, 17, 24, 51, DateTimeKind.Utc).AddTicks(4056), "Delta Exchange" });

            migrationBuilder.UpdateData(
                table: "Exchanges",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "BaseUrl", "CreatedAt", "Name" },
                values: new object[] { "https://api.cosmic.exchange", new DateTime(2026, 6, 13, 16, 17, 24, 51, DateTimeKind.Utc).AddTicks(4059), "Cosmic Exchange" });
        }
    }
}
