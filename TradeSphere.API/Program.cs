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
    context.Database.EnsureCreated();

    if (!context.Strategies.Any(s => s.Name == "HA-EMA Directional Bias Strategy"))
    {
        context.Strategies.Add(new TradeSphere.Domain.Entities.Strategy
        {
            Name = "HA-EMA Directional Bias Strategy",
            Description = "A directional momentum strategy using Daily Heikin Ashi candles for trend bias and 34-period EMA High/Low bands on intraday charts for crossovers, exits, and risk-reward management.",
            LogicType = "HA-EMA",
            DefaultConfig = "{\"emaLength\":34,\"dailyTimeframe\":\"1d\",\"resolution\":\"1h\",\"exitMode\":\"Band-Based Exit\",\"rrRatio\":2.0,\"useATRSL\":false,\"atrLength\":14,\"atrMultiplier\":1.5,\"sessionStart\":\"0915-1515\",\"squareOffHour\":15,\"squareOffMinute\":15}",
            IsPublic = true,
            CreatedBy = null
        });
        context.SaveChanges();
    }
}


app.Run();
