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
