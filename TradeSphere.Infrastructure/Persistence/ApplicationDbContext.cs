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

            modelBuilder.Entity<Strategy>()
                .HasOne(s => s.Creator)
                .WithMany()
                .HasForeignKey(s => s.CreatedBy)
                .IsRequired(false);

            // Seed Exchanges
            modelBuilder.Entity<Exchange>().HasData(
                new Exchange { Id = 1, Name = "Delta Exchange", BaseUrl = "https://api.delta.exchange", IsActive = true },
                new Exchange { Id = 2, Name = "Cosmic Exchange", BaseUrl = "https://api.cosmic.exchange", IsActive = true }
            );
        }
    }
}
