using SabemiTec.Api.ACL;
using SabemiTec.Api.ACL.PartnerBank;
using SabemiTec.Api.Database.PostgreSQL;
using SabemiTec.Api.Features.Dashboard.Repositories;
using SabemiTec.Api.Features.Processing;
using SabemiTec.Api.Features.Processing.Repositories;
using SabemiTec.Api.Features.Processing.Services;
using SabemiTec.Api.Features.Webhooks.Repositories;
using SabemiTec.Api.Features.Webhooks.Services;

namespace SabemiTec.Api.Configurations.Extensions;

/// <summary>
/// One method per area of responsibility, each returning IServiceCollection to allow the
/// fluent chain in Program.cs — same convention as core.flashcard-master.
/// </summary>
public static class ServicesExtensions
{
    // Same JSON-file chain WebApplication.CreateBuilder already sets up, made explicit so it's
    // easy to extend per environment. No AWS Systems Manager here — sabemi-tec keeps secrets in
    // plain environment variables (Render env vars), unlike core.flashcard-master's SSM setup.
    public static IConfigurationBuilder AddCustomConfiguration(
        this IConfigurationBuilder builder,
        IHostEnvironment env
    )
    {
        builder
            .SetBasePath(env.ContentRootPath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile(
                $"appsettings.{env.EnvironmentName}.json",
                optional: true,
                reloadOnChange: true
            );

        return builder.AddEnvironmentVariables();
    }

    public static IServiceCollection AddDatabase(this IServiceCollection services)
    {
        // Scoped, not singleton: IConfiguration is resolved from the request scope's
        // IServiceProvider at the moment each instance is created — same reasoning as the
        // core.flashcard-master DatabaseConnection classes. WebApplicationFactory applies its
        // config overrides during Build(), which happens before any scope is created, so this
        // is naturally safe for integration tests too.
        services.AddScoped<IDatabaseConnection, DatabaseConnection>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }

    public static IServiceCollection AddAcl(this IServiceCollection services)
    {
        services.AddSingleton<IPaymentWebhookAcl, PartnerBankWebhookAcl>();

        return services;
    }

    public static IServiceCollection AddWebhookFeature(this IServiceCollection services)
    {
        services.AddScoped<IPaymentEventRepository, PaymentEventRepository>();
        services.AddScoped<IngestPaymentHandler>();

        return services;
    }

    public static IServiceCollection AddProcessingFeature(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<ProcessingOptions>(
            configuration.GetSection(ProcessingOptions.SectionName)
        );
        services.AddScoped<IOutboxClaimRepository, OutboxClaimRepository>();
        services.AddScoped<IContractStatusRepository, ContractStatusRepository>();
        services.AddScoped<PaymentEventProcessor>();
        services.AddHostedService<PaymentEventProcessorWorker>();

        return services;
    }

    public static IServiceCollection AddDashboardFeature(this IServiceCollection services)
    {
        services.AddScoped<IPaymentQueryRepository, PaymentQueryRepository>();

        return services;
    }

    // Same method/policy name core.flashcard-master uses (AddCorsPolicy/"AllowSpecificOrigins"),
    // adapted to this app's actual shape: a single browser client (the dashboard) reading GET
    // endpoints with an X-Api-Key header — no AllowAnyHeader/AllowAnyMethod/AllowCredentials,
    // and origins stay config-driven (Cors:DashboardOrigins) instead of hardcoded per
    // environment, matching how the rest of this project configures things (RateLimiting,
    // Webhook:ApiKey). Cross-origin only where it has to be: on Render, nginx proxying /api to
    // the API service hits a TLS handshake failure against Render's own edge, so the dashboard
    // calls the API's public URL directly instead of same-origin through a proxy — see
    // web/src/api/client.ts. Locally (Vite dev, docker-compose) this policy is unused, since
    // same-origin requests never trigger CORS.
    public static IServiceCollection AddCorsPolicy(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var dashboardOrigins =
            configuration.GetSection("Cors:DashboardOrigins").Get<string[]>() ?? [];

        services.AddCors(options =>
        {
            options.AddPolicy(
                DashboardCorsPolicy,
                policy =>
                    policy.WithOrigins(dashboardOrigins).WithMethods("GET").WithHeaders("X-Api-Key")
            );
        });

        return services;
    }

    public const string DashboardCorsPolicy = "AllowSpecificOrigins";
}
