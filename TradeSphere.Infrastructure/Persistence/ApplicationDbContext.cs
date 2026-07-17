using System;
using Microsoft.EntityFrameworkCore;
using TradeSphere.Domain.Entities;

namespace TradeSphere.Infrastructure.Persistence
{
    public class ApplicationDbContext : DbContext
    {
        static ApplicationDbContext()
        {
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        }

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Exchange> Exchanges { get; set; }
        public DbSet<UserExchange> UserExchanges { get; set; }
        public DbSet<Strategy> Strategies { get; set; }
        public DbSet<UserStrategy> UserStrategies { get; set; }
        public DbSet<Trade> Trades { get; set; }
        public DbSet<Backtest> Backtests { get; set; }
        public DbSet<AiScreenerResult> AiScreenerResults { get; set; }
        public DbSet<Coin> Coins { get; set; }
        public DbSet<Mt5Account> Mt5Accounts { get; set; }
        public DbSet<Mt5SymbolMapping> Mt5SymbolMappings { get; set; }
        public DbSet<PropFirm> PropFirms { get; set; }
        public DbSet<PropFirmAccount> PropFirmAccounts { get; set; }
        public DbSet<StrategyHealthSnapshot> StrategyHealthSnapshots { get; set; }
        public DbSet<CryptoOptionStrategyConfig> CryptoOptionStrategyConfigs { get; set; }
        public DbSet<CryptoOptionStrategyLeg> CryptoOptionStrategyLegs { get; set; }
        public DbSet<CryptoOptionChainSnapshot> CryptoOptionChainSnapshots { get; set; }
        public DbSet<CryptoOptionBacktestRun> CryptoOptionBacktestRuns { get; set; }
        public DbSet<CryptoOptionBacktestPosition> CryptoOptionBacktestPositions { get; set; }
        public DbSet<CryptoOptionBacktestLeg> CryptoOptionBacktestLegs { get; set; }
        public DbSet<CryptoOptionBacktestLegEvent> CryptoOptionBacktestLegEvents { get; set; }
        public DbSet<CryptoOptionDailyPnl> CryptoOptionDailyPnls { get; set; }
        public DbSet<CryptoOptionScannerResult> CryptoOptionScannerResults { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username).IsUnique();
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email).IsUnique();

            modelBuilder.Entity<Exchange>()
                .HasIndex(e => e.Name).IsUnique();

            modelBuilder.Entity<Coin>()
                .HasIndex(c => c.Symbol).IsUnique();

            modelBuilder.Entity<Mt5Account>()
                .HasIndex(a => new { a.UserId, a.Login, a.Server }).IsUnique();

            modelBuilder.Entity<Mt5SymbolMapping>()
                .HasIndex(m => new { m.UserId, m.Mt5AccountId, m.StrategySymbol }).IsUnique();

            modelBuilder.Entity<PropFirm>()
                .HasIndex(f => new { f.UserId, f.Name }).IsUnique();

            modelBuilder.Entity<StrategyHealthSnapshot>()
                .HasIndex(h => h.UserStrategyId).IsUnique();

            modelBuilder.Entity<StrategyHealthSnapshot>()
                .HasOne(h => h.UserStrategy)
                .WithMany()
                .HasForeignKey(h => h.UserStrategyId)
                .OnDelete(DeleteBehavior.Cascade);


            modelBuilder.Entity<CryptoOptionStrategyConfig>()
                .HasIndex(c => new { c.UserId, c.Name }).IsUnique();

            modelBuilder.Entity<CryptoOptionStrategyLeg>()
                .HasOne(l => l.StrategyConfig)
                .WithMany(c => c.Legs)
                .HasForeignKey(l => l.StrategyConfigId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CryptoOptionChainSnapshot>()
                .HasIndex(s => new { s.Exchange, s.Symbol, s.ExpiryDate, s.SnapshotTime });

            modelBuilder.Entity<CryptoOptionChainSnapshot>()
                .HasIndex(s => new { s.Exchange, s.Symbol, s.ExpiryDate, s.Strike, s.SnapshotTime });

            modelBuilder.Entity<CryptoOptionBacktestRun>()
                .HasIndex(r => new { r.UserId, r.StartedAt });

            modelBuilder.Entity<CryptoOptionBacktestRun>()
                .HasOne(r => r.StrategyConfig)
                .WithMany()
                .HasForeignKey(r => r.StrategyConfigId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<CryptoOptionBacktestPosition>()
                .HasOne(p => p.BacktestRun)
                .WithMany(r => r.Positions)
                .HasForeignKey(p => p.BacktestRunId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CryptoOptionBacktestLeg>()
                .HasOne(l => l.BacktestPosition)
                .WithMany(p => p.Legs)
                .HasForeignKey(l => l.BacktestPositionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CryptoOptionBacktestLegEvent>()
                .HasOne(e => e.BacktestLeg)
                .WithMany(l => l.Events)
                .HasForeignKey(e => e.BacktestLegId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CryptoOptionDailyPnl>()
                .HasOne(p => p.BacktestRun)
                .WithMany(r => r.DailyPnls)
                .HasForeignKey(p => p.BacktestRunId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CryptoOptionScannerResult>()
                .HasIndex(r => new { r.UserId, r.Exchange, r.Symbol, r.ScanTime });
            modelBuilder.Entity<UserStrategy>()
                .HasOne(us => us.Mt5Account)
                .WithMany()
                .HasForeignKey(us => us.Mt5AccountId)
                .IsRequired(false);

            modelBuilder.Entity<Strategy>()
                .HasOne(s => s.Creator)
                .WithMany()
                .HasForeignKey(s => s.CreatedBy)
                .IsRequired(false);

            // Seed Exchanges
            modelBuilder.Entity<Exchange>().HasData(
                new Exchange { Id = 1, Name = "Delta Exchange India", BaseUrl = "https://api.india.delta.exchange", IsActive = true },
                new Exchange { Id = 2, Name = "Delta Exchange Global", BaseUrl = "https://api.delta.exchange", IsActive = true },
                new Exchange { Id = 3, Name = "Delta Exchange Testnet", BaseUrl = "https://testnet-api.delta.exchange", IsActive = true }
            );
        }
    }
}


