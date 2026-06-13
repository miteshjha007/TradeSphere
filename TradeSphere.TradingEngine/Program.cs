using TradeSphere.TradingEngine;
using TradeSphere.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

// Register Infrastructure (Database & Clients)
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHostedService<TradingWorker>();

var host = builder.Build();
host.Run();
