using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TradeSphere.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCryptoOptionsDesk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CryptoOptionChainSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Exchange = table.Column<string>(type: "text", nullable: false),
                    Underlying = table.Column<string>(type: "text", nullable: false),
                    Symbol = table.Column<string>(type: "text", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Strike = table.Column<decimal>(type: "numeric", nullable: false),
                    UnderlyingPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    SnapshotTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CallPremium = table.Column<decimal>(type: "numeric", nullable: true),
                    CallBid = table.Column<decimal>(type: "numeric", nullable: true),
                    CallAsk = table.Column<decimal>(type: "numeric", nullable: true),
                    CallVolume = table.Column<decimal>(type: "numeric", nullable: true),
                    CallOpenInterest = table.Column<decimal>(type: "numeric", nullable: true),
                    CallIv = table.Column<decimal>(type: "numeric", nullable: true),
                    CallDelta = table.Column<decimal>(type: "numeric", nullable: true),
                    CallGamma = table.Column<decimal>(type: "numeric", nullable: true),
                    CallTheta = table.Column<decimal>(type: "numeric", nullable: true),
                    CallVega = table.Column<decimal>(type: "numeric", nullable: true),
                    CallRho = table.Column<decimal>(type: "numeric", nullable: true),
                    PutPremium = table.Column<decimal>(type: "numeric", nullable: true),
                    PutBid = table.Column<decimal>(type: "numeric", nullable: true),
                    PutAsk = table.Column<decimal>(type: "numeric", nullable: true),
                    PutVolume = table.Column<decimal>(type: "numeric", nullable: true),
                    PutOpenInterest = table.Column<decimal>(type: "numeric", nullable: true),
                    PutIv = table.Column<decimal>(type: "numeric", nullable: true),
                    PutDelta = table.Column<decimal>(type: "numeric", nullable: true),
                    PutGamma = table.Column<decimal>(type: "numeric", nullable: true),
                    PutTheta = table.Column<decimal>(type: "numeric", nullable: true),
                    PutVega = table.Column<decimal>(type: "numeric", nullable: true),
                    PutRho = table.Column<decimal>(type: "numeric", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CryptoOptionChainSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CryptoOptionScannerResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Exchange = table.Column<string>(type: "text", nullable: false),
                    Symbol = table.Column<string>(type: "text", nullable: false),
                    ScannerMode = table.Column<string>(type: "text", nullable: false),
                    ScanTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    StrategyType = table.Column<string>(type: "text", nullable: false),
                    BestCallStrike = table.Column<decimal>(type: "numeric", nullable: true),
                    BestCallPremium = table.Column<decimal>(type: "numeric", nullable: true),
                    BestCallDelta = table.Column<decimal>(type: "numeric", nullable: true),
                    BestCallTheta = table.Column<decimal>(type: "numeric", nullable: true),
                    BestCallIv = table.Column<decimal>(type: "numeric", nullable: true),
                    BestPutStrike = table.Column<decimal>(type: "numeric", nullable: true),
                    BestPutPremium = table.Column<decimal>(type: "numeric", nullable: true),
                    BestPutDelta = table.Column<decimal>(type: "numeric", nullable: true),
                    BestPutTheta = table.Column<decimal>(type: "numeric", nullable: true),
                    BestPutIv = table.Column<decimal>(type: "numeric", nullable: true),
                    StrategyScore = table.Column<decimal>(type: "numeric", nullable: false),
                    ProbabilityOfProfit = table.Column<decimal>(type: "numeric", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CryptoOptionScannerResults", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CryptoOptionStrategyConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    StrategyType = table.Column<string>(type: "text", nullable: false),
                    Underlying = table.Column<string>(type: "text", nullable: false),
                    Symbol = table.Column<string>(type: "text", nullable: false),
                    Exchange = table.Column<string>(type: "text", nullable: false),
                    ExpiryType = table.Column<string>(type: "text", nullable: false),
                    EntryTime = table.Column<string>(type: "text", nullable: false),
                    ExitTime = table.Column<string>(type: "text", nullable: false),
                    TimeZone = table.Column<string>(type: "text", nullable: false),
                    TargetPremiumPerLeg = table.Column<decimal>(type: "numeric", nullable: false),
                    StopLossPercentPerLeg = table.Column<decimal>(type: "numeric", nullable: false),
                    StrikeSelectionMode = table.Column<string>(type: "text", nullable: false),
                    StrikeDistancePercent = table.Column<decimal>(type: "numeric", nullable: false),
                    MaxDailyLoss = table.Column<decimal>(type: "numeric", nullable: false),
                    LotSize = table.Column<decimal>(type: "numeric", nullable: false),
                    UseAtrFilter = table.Column<bool>(type: "boolean", nullable: false),
                    AtrLength = table.Column<int>(type: "integer", nullable: false),
                    MaxAtrPercent = table.Column<decimal>(type: "numeric", nullable: false),
                    UseTrendFilter = table.Column<bool>(type: "boolean", nullable: false),
                    EmaLength = table.Column<int>(type: "integer", nullable: false),
                    MaxTrendDistancePercent = table.Column<decimal>(type: "numeric", nullable: false),
                    UseSlippage = table.Column<bool>(type: "boolean", nullable: false),
                    SlippagePercent = table.Column<decimal>(type: "numeric", nullable: false),
                    BrokeragePerOrder = table.Column<decimal>(type: "numeric", nullable: false),
                    ExchangeFeePercent = table.Column<decimal>(type: "numeric", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CryptoOptionStrategyConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CryptoOptionBacktestRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    StrategyConfigId = table.Column<int>(type: "integer", nullable: true),
                    StrategyName = table.Column<string>(type: "text", nullable: false),
                    StrategyType = table.Column<string>(type: "text", nullable: false),
                    Symbol = table.Column<string>(type: "text", nullable: false),
                    Exchange = table.Column<string>(type: "text", nullable: false),
                    FromDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ToDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    InitialCapital = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalPnl = table.Column<decimal>(type: "numeric", nullable: false),
                    GrossPnl = table.Column<decimal>(type: "numeric", nullable: false),
                    Charges = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalTrades = table.Column<int>(type: "integer", nullable: false),
                    WinningDays = table.Column<int>(type: "integer", nullable: false),
                    LosingDays = table.Column<int>(type: "integer", nullable: false),
                    MaxDrawdown = table.Column<decimal>(type: "numeric", nullable: false),
                    ProfitFactor = table.Column<decimal>(type: "numeric", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CryptoOptionBacktestRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CryptoOptionBacktestRuns_CryptoOptionStrategyConfigs_Strate~",
                        column: x => x.StrategyConfigId,
                        principalTable: "CryptoOptionStrategyConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CryptoOptionStrategyLegs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StrategyConfigId = table.Column<int>(type: "integer", nullable: false),
                    LegName = table.Column<string>(type: "text", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    OptionType = table.Column<string>(type: "text", nullable: false),
                    ExpiryType = table.Column<string>(type: "text", nullable: false),
                    StrikeSelectionMode = table.Column<string>(type: "text", nullable: false),
                    TargetPremium = table.Column<decimal>(type: "numeric", nullable: false),
                    StrikeDistancePercent = table.Column<decimal>(type: "numeric", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CryptoOptionStrategyLegs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CryptoOptionStrategyLegs_CryptoOptionStrategyConfigs_Strate~",
                        column: x => x.StrategyConfigId,
                        principalTable: "CryptoOptionStrategyConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CryptoOptionBacktestPositions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BacktestRunId = table.Column<int>(type: "integer", nullable: false),
                    TradeDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Underlying = table.Column<string>(type: "text", nullable: false),
                    UnderlyingEntryPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    UnderlyingExitPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ExitReason = table.Column<string>(type: "text", nullable: false),
                    GrossPnl = table.Column<decimal>(type: "numeric", nullable: false),
                    NetPnl = table.Column<decimal>(type: "numeric", nullable: false),
                    Charges = table.Column<decimal>(type: "numeric", nullable: false),
                    IsCircuitBreakerHit = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CryptoOptionBacktestPositions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CryptoOptionBacktestPositions_CryptoOptionBacktestRuns_Back~",
                        column: x => x.BacktestRunId,
                        principalTable: "CryptoOptionBacktestRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CryptoOptionDailyPnls",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BacktestRunId = table.Column<int>(type: "integer", nullable: false),
                    TradeDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    GrossPnl = table.Column<decimal>(type: "numeric", nullable: false),
                    NetPnl = table.Column<decimal>(type: "numeric", nullable: false),
                    Charges = table.Column<decimal>(type: "numeric", nullable: false),
                    MaxIntradayLoss = table.Column<decimal>(type: "numeric", nullable: false),
                    CeLegPnl = table.Column<decimal>(type: "numeric", nullable: false),
                    PeLegPnl = table.Column<decimal>(type: "numeric", nullable: false),
                    IsCircuitBreakerHit = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CryptoOptionDailyPnls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CryptoOptionDailyPnls_CryptoOptionBacktestRuns_BacktestRunId",
                        column: x => x.BacktestRunId,
                        principalTable: "CryptoOptionBacktestRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CryptoOptionBacktestLegs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BacktestPositionId = table.Column<int>(type: "integer", nullable: false),
                    LegType = table.Column<string>(type: "text", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    Strike = table.Column<decimal>(type: "numeric", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EntryTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EntryPremium = table.Column<decimal>(type: "numeric", nullable: false),
                    ExitTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ExitPremium = table.Column<decimal>(type: "numeric", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    Pnl = table.Column<decimal>(type: "numeric", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ExitReason = table.Column<string>(type: "text", nullable: false),
                    StopLossHit = table.Column<bool>(type: "boolean", nullable: false),
                    EntryDelta = table.Column<decimal>(type: "numeric", nullable: true),
                    EntryTheta = table.Column<decimal>(type: "numeric", nullable: true),
                    EntryIv = table.Column<decimal>(type: "numeric", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CryptoOptionBacktestLegs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CryptoOptionBacktestLegs_CryptoOptionBacktestPositions_Back~",
                        column: x => x.BacktestPositionId,
                        principalTable: "CryptoOptionBacktestPositions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CryptoOptionBacktestLegEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BacktestLegId = table.Column<int>(type: "integer", nullable: false),
                    EventTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    Premium = table.Column<decimal>(type: "numeric", nullable: false),
                    Pnl = table.Column<decimal>(type: "numeric", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CryptoOptionBacktestLegEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CryptoOptionBacktestLegEvents_CryptoOptionBacktestLegs_Back~",
                        column: x => x.BacktestLegId,
                        principalTable: "CryptoOptionBacktestLegs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Exchanges",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 6, 27, 12, 50, 6, 80, DateTimeKind.Utc).AddTicks(6165));

            migrationBuilder.UpdateData(
                table: "Exchanges",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 6, 27, 12, 50, 6, 80, DateTimeKind.Utc).AddTicks(6169));

            migrationBuilder.UpdateData(
                table: "Exchanges",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 6, 27, 12, 50, 6, 80, DateTimeKind.Utc).AddTicks(6170));

            migrationBuilder.CreateIndex(
                name: "IX_CryptoOptionBacktestLegEvents_BacktestLegId",
                table: "CryptoOptionBacktestLegEvents",
                column: "BacktestLegId");

            migrationBuilder.CreateIndex(
                name: "IX_CryptoOptionBacktestLegs_BacktestPositionId",
                table: "CryptoOptionBacktestLegs",
                column: "BacktestPositionId");

            migrationBuilder.CreateIndex(
                name: "IX_CryptoOptionBacktestPositions_BacktestRunId",
                table: "CryptoOptionBacktestPositions",
                column: "BacktestRunId");

            migrationBuilder.CreateIndex(
                name: "IX_CryptoOptionBacktestRuns_StrategyConfigId",
                table: "CryptoOptionBacktestRuns",
                column: "StrategyConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_CryptoOptionBacktestRuns_UserId_StartedAt",
                table: "CryptoOptionBacktestRuns",
                columns: new[] { "UserId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CryptoOptionChainSnapshots_Exchange_Symbol_ExpiryDate_Snaps~",
                table: "CryptoOptionChainSnapshots",
                columns: new[] { "Exchange", "Symbol", "ExpiryDate", "SnapshotTime" });

            migrationBuilder.CreateIndex(
                name: "IX_CryptoOptionChainSnapshots_Exchange_Symbol_ExpiryDate_Strik~",
                table: "CryptoOptionChainSnapshots",
                columns: new[] { "Exchange", "Symbol", "ExpiryDate", "Strike", "SnapshotTime" });

            migrationBuilder.CreateIndex(
                name: "IX_CryptoOptionDailyPnls_BacktestRunId",
                table: "CryptoOptionDailyPnls",
                column: "BacktestRunId");

            migrationBuilder.CreateIndex(
                name: "IX_CryptoOptionScannerResults_UserId_Exchange_Symbol_ScanTime",
                table: "CryptoOptionScannerResults",
                columns: new[] { "UserId", "Exchange", "Symbol", "ScanTime" });

            migrationBuilder.CreateIndex(
                name: "IX_CryptoOptionStrategyConfigs_UserId_Name",
                table: "CryptoOptionStrategyConfigs",
                columns: new[] { "UserId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CryptoOptionStrategyLegs_StrategyConfigId",
                table: "CryptoOptionStrategyLegs",
                column: "StrategyConfigId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CryptoOptionBacktestLegEvents");

            migrationBuilder.DropTable(
                name: "CryptoOptionChainSnapshots");

            migrationBuilder.DropTable(
                name: "CryptoOptionDailyPnls");

            migrationBuilder.DropTable(
                name: "CryptoOptionScannerResults");

            migrationBuilder.DropTable(
                name: "CryptoOptionStrategyLegs");

            migrationBuilder.DropTable(
                name: "CryptoOptionBacktestLegs");

            migrationBuilder.DropTable(
                name: "CryptoOptionBacktestPositions");

            migrationBuilder.DropTable(
                name: "CryptoOptionBacktestRuns");

            migrationBuilder.DropTable(
                name: "CryptoOptionStrategyConfigs");

            migrationBuilder.UpdateData(
                table: "Exchanges",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 6, 24, 10, 56, 26, 157, DateTimeKind.Utc).AddTicks(3877));

            migrationBuilder.UpdateData(
                table: "Exchanges",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 6, 24, 10, 56, 26, 157, DateTimeKind.Utc).AddTicks(3883));

            migrationBuilder.UpdateData(
                table: "Exchanges",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 6, 24, 10, 56, 26, 157, DateTimeKind.Utc).AddTicks(3884));
        }
    }
}
