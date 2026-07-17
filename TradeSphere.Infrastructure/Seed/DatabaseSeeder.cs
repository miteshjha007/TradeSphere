using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TradeSphere.Domain.Entities;
using TradeSphere.Infrastructure.Persistence;

namespace TradeSphere.Infrastructure.Seed
{
    public static class DatabaseSeeder
    {
        private const string ExchangePath = "SeedData/exchanges.json";
        private const string StrategyTemplatePath = "SeedData/strategy-templates.json";

        public static async Task MigrateAndSeedAsync(IServiceProvider serviceProvider, string contentRootPath, CancellationToken cancellationToken = default)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            await context.Database.MigrateAsync(cancellationToken);
            await SeedExchangesAsync(context, contentRootPath, cancellationToken);
            await SeedStrategyTemplatesAsync(context, contentRootPath, cancellationToken);
        }

        private static async Task SeedExchangesAsync(ApplicationDbContext context, string contentRootPath, CancellationToken cancellationToken)
        {
            var filePath = Path.Combine(contentRootPath, ExchangePath);
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Exchange seed file was not found at '{filePath}'.", filePath);
            }

            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            var exchanges = JsonSerializer.Deserialize<List<ExchangeSeed>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<ExchangeSeed>();

            foreach (var seed in exchanges)
            {
                var exchange = await context.Exchanges
                    .FirstOrDefaultAsync(e => e.Name == seed.Name, cancellationToken);

                if (exchange == null)
                {
                    context.Exchanges.Add(new Exchange
                    {
                        Name = seed.Name,
                        BaseUrl = seed.BaseUrl,
                        IsActive = seed.IsActive
                    });
                    continue;
                }

                exchange.BaseUrl = seed.BaseUrl;
                exchange.IsActive = seed.IsActive;
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        private static async Task SeedStrategyTemplatesAsync(ApplicationDbContext context, string contentRootPath, CancellationToken cancellationToken)
        {
            var filePath = Path.Combine(contentRootPath, StrategyTemplatePath);
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Strategy seed file was not found at '{filePath}'.", filePath);
            }

            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            var templates = JsonSerializer.Deserialize<List<StrategyTemplateSeed>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<StrategyTemplateSeed>();

            var staleTemplateLogicTypes = new[] { "FIB-55-EMA-V3" };
            var staleTemplates = await context.Strategies
                .Where(s => s.CreatedBy == null && staleTemplateLogicTypes.Contains(s.LogicType))
                .ToListAsync(cancellationToken);

            if (staleTemplates.Count > 0)
            {
                context.Strategies.RemoveRange(staleTemplates);
            }
            foreach (var template in templates)
            {
                var strategy = await context.Strategies
                    .FirstOrDefaultAsync(s =>
                        s.CreatedBy == null &&
                        (s.LogicType == template.LogicType || s.Name == template.Name),
                        cancellationToken);

                var defaultConfig = template.DefaultConfig.GetRawText();
                if (strategy == null)
                {
                    context.Strategies.Add(new Strategy
                    {
                        Name = template.Name,
                        Description = template.Description,
                        LogicType = template.LogicType,
                        DefaultConfig = defaultConfig,
                        IsPublic = template.IsPublic,
                        CreatedBy = null
                    });
                    continue;
                }

                strategy.Name = template.Name;
                strategy.Description = template.Description;
                strategy.LogicType = template.LogicType;
                strategy.DefaultConfig = defaultConfig;
                strategy.IsPublic = template.IsPublic;
                strategy.UpdatedAt = DateTime.UtcNow;
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        private sealed class StrategyTemplateSeed
        {
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string LogicType { get; set; } = string.Empty;
            public JsonElement DefaultConfig { get; set; }
            public bool IsPublic { get; set; } = true;
        }

        private sealed class ExchangeSeed
        {
            public string Name { get; set; } = string.Empty;
            public string BaseUrl { get; set; } = string.Empty;
            public bool IsActive { get; set; } = true;
        }
    }
}
