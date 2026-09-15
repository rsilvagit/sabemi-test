using SabemiTec.Api.Configurations.Extensions;
using SabemiTec.Api.Persistence;
using SabemiTec.Api.Persistence.Migrations;
using SabemiTec.Api.Security.RateLimiting;

DapperConfig.Configure();

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddEndpointsApiExplorer()
    .AddSwaggerGen()
    .AddPersistence(builder.Configuration)
    .AddAcl()
    .AddWebhookFeature()
    .AddProcessingFeature(builder.Configuration)
    .AddDashboardFeature()
    .AddWebhookRateLimiting(builder.Configuration);

var app = builder.Build();

if (!app.Environment.IsEnvironment("Testing"))
{
    var connectionString = builder.Configuration.GetConnectionString("Default")!;
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Migrations");
    await DatabaseMigrator.MigrateAsync(connectionString, logger, app.Lifetime.ApplicationStopping);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRateLimiter();

app.ConfigureMapsApp();

app.Run();

public partial class Program;
