using Npgsql;
using SabemiTec.Api.ACL;
using SabemiTec.Api.ACL.PartnerBank;
using SabemiTec.Api.Features.Dashboard.Repositories;
using SabemiTec.Api.Features.Processing;
using SabemiTec.Api.Features.Processing.Repositories;
using SabemiTec.Api.Features.Processing.Services;
using SabemiTec.Api.Features.Webhooks.Repositories;
using SabemiTec.Api.Features.Webhooks.Services;
using SabemiTec.Api.Persistence;
using SabemiTec.Api.Security;

namespace SabemiTec.Api.Configurations.Extensions;

/// <summary>
/// One method per area of responsibility, each returning IServiceCollection to allow the
/// fluent chain in Program.cs — same convention as core.flashcard-master.
/// </summary>
public static class ServicesExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        // Lazy resolution: reads IConfiguration from the final IServiceProvider, at the
        // moment the singleton is created — not from builder.Configuration at registration
        // time. WebApplicationFactory injects its config overrides during Build(), which
        // happens AFTER this method runs; capturing the connection string here instead of
        // reading it from the IServiceProvider would make integration tests connect to the
        // wrong string.
        services.AddSingleton(sp =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("Default")
                ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");
            return NpgsqlDataSource.Create(connectionString);
        });
        services.AddScoped<IUnitOfWork, NpgsqlUnitOfWork>();

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
        services.AddTransient<ApiKeyEndpointFilter>();

        return services;
    }

    public static IServiceCollection AddProcessingFeature(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ProcessingOptions>(configuration.GetSection(ProcessingOptions.SectionName));
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
}
