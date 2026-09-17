using FluentValidation;
using Microsoft.OpenApi.Models;
using SabemiTec.Api.Configurations.Extensions;
using SabemiTec.Api.Configurations.RateLimiting;
using SabemiTec.Api.Database.PostgreSQL;
using SabemiTec.Api.Database.PostgreSQL.Migrations;
using SabemiTec.Api.Middlewares;

DapperConfig.Configure();

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddCustomConfiguration(builder.Environment);

// One-shot mode: `dotnet SabemiTec.Api.dll migrate` applies the schema and exits, instead of
// starting the web host — used by the `migrate` service in docker-compose.yml, which must
// finish before `api` starts so a fresh local Postgres always has the schema in place.
if (args.Contains("migrate", StringComparer.OrdinalIgnoreCase))
{
    using var loggerFactory = LoggerFactory.Create(logging => logging.AddConsole());
    var migrationLogger = loggerFactory.CreateLogger("Migrations");
    var connectionString = builder.Configuration.GetConnectionString("Default")!;

    await DatabaseMigrator.MigrateAsync(connectionString, migrationLogger, CancellationToken.None);
    return;
}

builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);
builder.Services.AddSwaggerGen(options =>
{
    // Lets Swagger UI's "Authorize" button collect the same X-Api-Key the
    // ApiKeyAuthMiddleware checks (webhook + dashboard routes), instead of every
    // "Try it out" call silently getting 401 with no way to attach the header.
    const string apiKeyScheme = "ApiKey";
    options.AddSecurityDefinition(apiKeyScheme, new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Name = "X-Api-Key",
        Description = "Chave do webhook (Webhook:ApiKey) — mesma usada pelo dashboard.",
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = apiKeyScheme } }] = []
    });
});

builder
    .Services.AddEndpointsApiExplorer()
    .AddDatabase()
    .AddAcl()
    .AddWebhookFeature()
    .AddProcessingFeature(builder.Configuration)
    .AddDashboardFeature()
    .AddRateLimiting(builder.Configuration)
    .AddCorsPolicy(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Before UseApiKeyAuth: CORS preflight (OPTIONS) requests never carry X-Api-Key, so the
// auth middleware would reject them before the browser gets to see the real response.
app.UseCors();

app.UseApiKeyAuth();

app.UseRateLimiter();

app.ConfigureMapsApp();

app.Run();
