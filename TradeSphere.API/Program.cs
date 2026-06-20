using System.Linq;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using TradeSphere.Application;
using TradeSphere.Infrastructure;
using TradeSphere.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// JWT Authentication
var key = Encoding.ASCII.GetBytes(builder.Configuration["Jwt:Key"] ?? "DefaultSecretKeyForDevelopment_ChangeMeInProduction");
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = false,
        ValidateAudience = false
    };
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder => builder
        .AllowAnyOrigin()
        .AllowAnyMethod()
        .AllowAnyHeader());
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Ensure Database Migration
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    context.Database.Migrate();

    var haEmaStrategy = context.Strategies.FirstOrDefault(s => s.Name == "HA-EMA Directional Bias Strategy");
    if (haEmaStrategy == null)
    {
        context.Strategies.Add(new TradeSphere.Domain.Entities.Strategy
        {
            Name = "HA-EMA Directional Bias Strategy",
            Description = "A directional momentum strategy using Daily Heikin Ashi candles for trend bias and 34-period EMA High/Low bands on intraday charts for crossovers, exits, and risk-reward management.",
            LogicType = "HA-EMA",
            DefaultConfig = "{\"emaLength\":34,\"dailyTimeframe\":\"1d\",\"resolution\":\"3m\",\"exitMode\":\"Band-Based Exit\",\"rrRatio\":2.0,\"useATRSL\":false,\"atrLength\":14,\"atrMultiplier\":1.5,\"sessionStart\":\"0915-1515\",\"squareOffHour\":15,\"squareOffMinute\":15,\"tradeSizeType\":\"Contracts\",\"tradeSizeValue\":1.0,\"leverage\":10.0}",
            IsPublic = true,
            CreatedBy = null
        });
        context.SaveChanges();
    }
    else if (!string.IsNullOrWhiteSpace(haEmaStrategy.DefaultConfig) && !haEmaStrategy.DefaultConfig.Contains("\"resolution\":\"3m\""))
    {
        haEmaStrategy.DefaultConfig = "{\"emaLength\":34,\"dailyTimeframe\":\"1d\",\"resolution\":\"3m\",\"exitMode\":\"Band-Based Exit\",\"rrRatio\":2.0,\"useATRSL\":false,\"atrLength\":14,\"atrMultiplier\":1.5,\"sessionStart\":\"0915-1515\",\"squareOffHour\":15,\"squareOffMinute\":15,\"tradeSizeType\":\"Contracts\",\"tradeSizeValue\":1.0,\"leverage\":10.0}";
        context.SaveChanges();
    }

    var fibEmaStrategy = context.Strategies.FirstOrDefault(s => s.Name == "Fib + 55 EMA V2 [Trend-Following Enhanced]");
    if (fibEmaStrategy == null)
    {
        context.Strategies.Add(new TradeSphere.Domain.Entities.Strategy
        {
            Name = "Fib + 55 EMA V2 [Trend-Following Enhanced]",
            Description = "A strict trend-following strategy using 55 EMA alignment, higher-timeframe trend confirmation, hourly Fibonacci pullback zones, RSI, volume, candle-strength filters, cooldown, and staged SL/TP exits.",
            LogicType = "FIB-55-EMA",
            DefaultConfig = "{\"emaLength\":55,\"htfTimeframe\":\"60\",\"resolution\":\"5m\",\"fib381\":0.382,\"fib500\":0.5,\"fib618\":0.618,\"zoneBuffer\":0.0015,\"minBodyPct\":30.0,\"cooldownBars\":5,\"rsiBuyMin\":40,\"rsiSellMax\":60,\"volumeMultiplier\":1.0,\"tp1RiskReward\":1.5,\"tp2RiskReward\":2.0,\"stopLossPct\":0.004,\"tradeSizeType\":\"Contracts\",\"tradeSizeValue\":1.0,\"leverage\":10.0}",
            IsPublic = true,
            CreatedBy = null
        });
        context.SaveChanges();
    }

    var pdLiquiditySweepStrategy = context.Strategies.FirstOrDefault(s => s.Name == "Previous Day Liquidity Sweep Scalping Strategy");
    if (pdLiquiditySweepStrategy == null)
    {
        context.Strategies.Add(new TradeSphere.Domain.Entities.Strategy
        {
            Name = "Previous Day Liquidity Sweep Scalping Strategy",
            Description = "A scalping strategy that watches previous day high/low liquidity sweeps, waits for rejection confirmation, enters on confirmation candle break, and manages risk with TP1/TP2 exits plus daily trade and loss limits.",
            LogicType = "PD-LIQUIDITY-SWEEP",
            DefaultConfig = "{\"resolution\":\"5m\",\"enableLong\":true,\"enableShort\":true,\"tp1RiskReward\":5.0,\"tp2RiskReward\":10.0,\"tp1ExitQtyPct\":80.0,\"maxTradesPerDay\":3,\"maxLossesPerDay\":2,\"minSweepPoints\":0.0,\"useSession\":false,\"tradeSession\":\"0000-2359\",\"showLevels\":true,\"tradeSizeType\":\"Contracts\",\"tradeSizeValue\":1.0,\"leverage\":10.0}",
            IsPublic = true,
            CreatedBy = null
        });
        context.SaveChanges();
    }

    var smcMultiTfStrategy = context.Strategies.FirstOrDefault(s => s.Name == "SMC Multi-TF Strategy | D-L-E Framework");
    if (smcMultiTfStrategy == null)
    {
        context.Strategies.Add(new TradeSphere.Domain.Entities.Strategy
        {
            Name = "SMC Multi-TF Strategy | D-L-E Framework",
            Description = "A Smart Money Concepts D-L-E framework strategy using higher-timeframe BOS for direction, medium-timeframe supply/demand POI for location, and lower-timeframe engulfing or pin-bar rejection for execution with risk-percent sizing and RR exits.",
            LogicType = "SMC-DLE-MULTI-TF",
            DefaultConfig = "{\"resolution\":\"15m\",\"higherTimeframe\":\"240\",\"mediumTimeframe\":\"60\",\"swingLookbackLength\":5,\"zoneWickTolerancePct\":30.0,\"requireEngulfing\":true,\"requirePinbar\":true,\"pinbarWickToBodyRatio\":2.0,\"requireBiasCloseAfterRejection\":false,\"riskPerTradePct\":1.0,\"riskRewardRatio\":3.0,\"maxTradesPerDay\":3,\"showBos\":true,\"showEquilibrium\":true,\"showPoiZones\":true,\"showPremiumDiscount\":true,\"showDebug\":true,\"tradeSizeType\":\"RiskPercent\",\"tradeSizeValue\":1.0,\"leverage\":10.0}",
            IsPublic = true,
            CreatedBy = null
        });
        context.SaveChanges();
    }
}


app.Run();
