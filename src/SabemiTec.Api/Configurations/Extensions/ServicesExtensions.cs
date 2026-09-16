using SabemiTec.Api.ACL;
using SabemiTec.Api.ACL.PartnerBank;
using SabemiTec.Api.Database.PostgreSQL;
using SabemiTec.Api.Features.Dashboard.Repositories;
using SabemiTec.Api.Features.Processing;
using SabemiTec.Api.Features.Processing.Repositories;
using SabemiTec.Api.Features.Processing.Services;
using SabemiTec.Api.Features.Webhooks.Repositories;
using SabemiTec.Api.Features.Webhooks.Services;
using SabemiTec.Api.Security;

namespace SabemiTec.Api.Configurations.Extensions;

/// <summary>
/// One method per area of responsibility, each returning IServiceCollection to allow the
/// fluent chain in Program.cs — same convention as core.flashcard-master.
/// </summary>
public static class ServicesExtensions
{
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
