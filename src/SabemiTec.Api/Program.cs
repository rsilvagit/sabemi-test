using SabemiTec.Api.Configurations.Extensions;
using SabemiTec.Api.Configurations.RateLimiting;
using SabemiTec.Api.Database.PostgreSQL;
using SabemiTec.Api.Database.PostgreSQL.Migrations;
using SabemiTec.Api.Middlewares;

DapperConfig.Configure();

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddEndpointsApiExplorer()
    .AddSwaggerGen()
    .AddDatabase()
    .AddAcl()
    .AddWebhookFeature()
    .AddProcessingFeature(builder.Configuration)
    .AddDashboardFeature()
    .AddRateLimiting(builder.Configuration);

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

app.UseApiKeyAuth();

app.UseRateLimiter();

app.ConfigureMapsApp();

app.Run();

public partial class Program;
