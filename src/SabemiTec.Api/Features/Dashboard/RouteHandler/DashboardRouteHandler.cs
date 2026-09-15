using SabemiTec.Api.Features.Dashboard.Repositories;

namespace SabemiTec.Api.Features.Dashboard.RouteHandler;

public static class DashboardRouteHandler
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payments").WithTags("Dashboard");

        group.MapGet("/", SearchAsync)
            .WithName("SearchPayments")
            .Produces(StatusCodes.Status200OK)
            .WithOpenApi();

        group.MapGet("/stats", GetStatsAsync)
            .WithName("GetPaymentStats")
            .Produces(StatusCodes.Status200OK)
            .WithOpenApi();

        group.MapGet("/{id:long}", GetByIdAsync)
            .WithName("GetPaymentById")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithOpenApi();

        return app;
    }

    private static async Task<IResult> SearchAsync(
        IPaymentQueryRepository repository,
        CancellationToken ct,
        string? status = null,
        string? contractId = null,
        int page = 1,
        int pageSize = 25)
    {
        var query = new PaymentQuery(status, contractId, page <= 0 ? 1 : page, pageSize <= 0 ? 25 : pageSize);
        var (items, hasMore) = await repository.SearchAsync(query, ct);

        return Results.Ok(new
        {
            items = items.Select(ToDto),
            page = query.Page,
            pageSize = query.PageSize,
            hasMore
        });
    }

    private static async Task<IResult> GetByIdAsync(long id, IPaymentQueryRepository repository, CancellationToken ct)
    {
        var detail = await repository.FindByIdAsync(id, ct);
        return detail is null ? Results.NotFound() : Results.Ok(ToDetailDto(detail));
    }

    private static async Task<IResult> GetStatsAsync(IPaymentQueryRepository repository, CancellationToken ct)
    {
        var stats = await repository.GetStatsAsync(ct);
        return Results.Ok(new
        {
            total = stats.Total,
            success = stats.Success,
            error = stats.Error,
            pending = stats.Pending
        });
    }

    private static object ToDto(PaymentListItem item) => new
    {
        id = item.Id,
        transactionId = item.TransactionId,
        contractId = item.ContractId,
        amount = item.Amount,
        paymentDate = item.PaymentDate,
        paymentStatus = item.PaymentStatus,
        processingStatus = item.ProcessingStatus,
        effectiveStatus = item.EffectiveStatus,
        attempts = item.Attempts,
        lastError = item.LastError,
        validationError = item.ValidationError,
        receivedAt = item.ReceivedAt,
        processedAt = item.ProcessedAt
    };

    private static object ToDetailDto(PaymentDetail item) => new
    {
        id = item.Id,
        transactionId = item.TransactionId,
        contractId = item.ContractId,
        amount = item.Amount,
        paymentDate = item.PaymentDate,
        paymentStatus = item.PaymentStatus,
        processingStatus = item.ProcessingStatus,
        effectiveStatus = item.EffectiveStatus,
        attempts = item.Attempts,
        lastError = item.LastError,
        validationError = item.ValidationError,
        receivedAt = item.ReceivedAt,
        processedAt = item.ProcessedAt,
        rawPayload = item.Payload
    };
}
