using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TradeSphere.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMt5AndPropFirmModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Mt5Accounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Login = table.Column<long>(type: "bigint", nullable: false),
                    Server = table.Column<string>(type: "text", nullable: false),
                    EncryptedPassword = table.Column<string>(type: "text", nullable: false),
                    AccountType = table.Column<string>(type: "text", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    Leverage = table.Column<int>(type: "integer", nullable: false),
                    TradingEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Balance = table.Column<decimal>(type: "numeric", nullable: true),
                    Equity = table.Column<decimal>(type: "numeric", nullable: true),
                    FreeMargin = table.Column<decimal>(type: "numeric", nullable: true),
                    LastSyncedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastError = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mt5Accounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Mt5Accounts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PropFirms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    WebsiteUrl = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropFirms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropFirms_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Mt5SymbolMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Mt5AccountId = table.Column<int>(type: "integer", nullable: false),
                    StrategySymbol = table.Column<string>(type: "text", nullable: false),
                    BrokerSymbol = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mt5SymbolMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Mt5SymbolMappings_Mt5Accounts_Mt5AccountId",
                        column: x => x.Mt5AccountId,
                        principalTable: "Mt5Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Mt5SymbolMappings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PropFirmAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    PropFirmId = table.Column<int>(type: "integer", nullable: false),
                    Mt5AccountId = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    AccountSize = table.Column<decimal>(type: "numeric", nullable: false),
                    ProfitTarget = table.Column<decimal>(type: "numeric", nullable: false),
                    DailyDrawdownLimit = table.Column<decimal>(type: "numeric", nullable: false),
                    MaxDrawdownLimit = table.Column<decimal>(type: "numeric", nullable: false),
                    MinimumTradingDays = table.Column<int>(type: "integer", nullable: false),
                    MaxRiskPerTradePercent = table.Column<decimal>(type: "numeric", nullable: false),
                    NewsTradingAllowed = table.Column<bool>(type: "boolean", nullable: false),
                    WeekendHoldingAllowed = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropFirmAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropFirmAccounts_Mt5Accounts_Mt5AccountId",
                        column: x => x.Mt5AccountId,
                        principalTable: "Mt5Accounts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PropFirmAccounts_PropFirms_PropFirmId",
                        column: x => x.PropFirmId,
                        principalTable: "PropFirms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PropFirmAccounts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_Mt5Accounts_UserId_Login_Server",
                table: "Mt5Accounts",
                columns: new[] { "UserId", "Login", "Server" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Mt5SymbolMappings_Mt5AccountId",
                table: "Mt5SymbolMappings",
                column: "Mt5AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Mt5SymbolMappings_UserId_Mt5AccountId_StrategySymbol",
                table: "Mt5SymbolMappings",
                columns: new[] { "UserId", "Mt5AccountId", "StrategySymbol" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PropFirmAccounts_Mt5AccountId",
                table: "PropFirmAccounts",
                column: "Mt5AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_PropFirmAccounts_PropFirmId",
                table: "PropFirmAccounts",
                column: "PropFirmId");

            migrationBuilder.CreateIndex(
                name: "IX_PropFirmAccounts_UserId",
                table: "PropFirmAccounts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PropFirms_UserId_Name",
                table: "PropFirms",
                columns: new[] { "UserId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Mt5SymbolMappings");

            migrationBuilder.DropTable(
                name: "PropFirmAccounts");

            migrationBuilder.DropTable(
                name: "Mt5Accounts");

            migrationBuilder.DropTable(
                name: "PropFirms");

            migrationBuilder.UpdateData(
                table: "Exchanges",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 6, 14, 4, 45, 55, 897, DateTimeKind.Utc).AddTicks(1845));

            migrationBuilder.UpdateData(
                table: "Exchanges",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 6, 14, 4, 45, 55, 897, DateTimeKind.Utc).AddTicks(1849));

            migrationBuilder.UpdateData(
                table: "Exchanges",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 6, 14, 4, 45, 55, 897, DateTimeKind.Utc).AddTicks(1850));
        }
    }
}
