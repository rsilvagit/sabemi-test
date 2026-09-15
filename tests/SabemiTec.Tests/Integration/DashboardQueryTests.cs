using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace SabemiTec.Tests.Integration;

[Collection(DatabaseCollection.Name)]
public class DashboardQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private SabemiWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await db.ResetAsync();
        _factory = new SabemiWebApplicationFactory(db.ConnectionString, workerEnabled: true, simulatedDelayMs: 0);
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Api-Key", SabemiWebApplicationFactory.TestApiKey);
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task InvalidPayload_ShowsUpAsError_AndCarriesTheValidationMessage()
    {
        await _client.PostAsJsonAsync("/webhooks/pagamento", new
        {
            id_transacao = "TX-DASH-001",
            id_contrato = "CT-DASH-01",
            valor = -10,
            data_pagamento = "2026-09-15T10:00:00Z",
            status = "PAGO"
        });

        var response = await _client.GetAsync("/api/payments?status=Error");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElementWrapper>(JsonOptions);
        var items = body!.Items;

        items.Should().ContainSingle(i => i.TransactionId == "TX-DASH-001");
        var item = items.Single(i => i.TransactionId == "TX-DASH-001");
        item.EffectiveStatus.Should().Be("Error");
        item.ValidationError.Should().Contain("valor");
    }

    [Fact]
    public async Task FilterByContractId_ReturnsOnlyMatchingContract()
    {
        await _client.PostAsJsonAsync("/webhooks/pagamento", new
        {
            id_transacao = "TX-DASH-010",
            id_contrato = "CT-A",
            valor = 10,
            data_pagamento = "2026-09-15T10:00:00Z",
            status = "PAGO"
        });
        await _client.PostAsJsonAsync("/webhooks/pagamento", new
        {
            id_transacao = "TX-DASH-011",
            id_contrato = "CT-B",
            valor = 10,
            data_pagamento = "2026-09-15T10:00:00Z",
            status = "PAGO"
        });

        var response = await _client.GetAsync("/api/payments?contractId=CT-A");
        var body = await response.Content.ReadFromJsonAsync<JsonElementWrapper>(JsonOptions);

        body!.Items.Should().OnlyContain(i => i.ContractId == "CT-A");
    }

    [Fact]
    public async Task Pagination_ReportsHasMoreCorrectly()
    {
        for (var i = 0; i < 3; i++)
        {
            await _client.PostAsJsonAsync("/webhooks/pagamento", new
            {
                id_transacao = $"TX-PAGE-{i}",
                id_contrato = "CT-PAGE",
                valor = 10,
                data_pagamento = "2026-09-15T10:00:00Z",
                status = "PAGO"
            });
        }

        var firstPage = await _client.GetAsync("/api/payments?contractId=CT-PAGE&page=1&pageSize=2");
        var firstBody = await firstPage.Content.ReadFromJsonAsync<JsonElementWrapper>(JsonOptions);
        firstBody!.Items.Should().HaveCount(2);
        firstBody.HasMore.Should().BeTrue();

        var secondPage = await _client.GetAsync("/api/payments?contractId=CT-PAGE&page=2&pageSize=2");
        var secondBody = await secondPage.Content.ReadFromJsonAsync<JsonElementWrapper>(JsonOptions);
        secondBody!.Items.Should().HaveCount(1);
        secondBody.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task GetById_ReturnsRawPayload()
    {
        var post = await _client.PostAsJsonAsync("/webhooks/pagamento", new
        {
            id_transacao = "TX-DASH-020",
            id_contrato = "CT-DASH-02",
            valor = 42,
            data_pagamento = "2026-09-15T10:00:00Z",
            status = "PAGO"
        });
        var created = await post.Content.ReadFromJsonAsync<CreatedResponse>(JsonOptions);

        var response = await _client.GetAsync($"/api/payments/{created!.Id}");
        response.EnsureSuccessStatusCode();

        var detail = await response.Content.ReadFromJsonAsync<PaymentDetailResponse>(JsonOptions);
        detail!.RawPayload.Should().Contain("TX-DASH-020");
    }

    [Fact]
    public async Task GetStats_CountsByEffectiveStatus()
    {
        await _client.PostAsJsonAsync("/webhooks/pagamento", new
        {
            id_transacao = "TX-STATS-OK",
            id_contrato = "CT-STATS",
            valor = 10,
            data_pagamento = "2026-09-15T10:00:00Z",
            status = "PAGO"
        });
        await _client.PostAsJsonAsync("/webhooks/pagamento", new
        {
            id_transacao = "TX-STATS-BAD",
            id_contrato = "CT-STATS",
            valor = -1,
            data_pagamento = "2026-09-15T10:00:00Z",
            status = "PAGO"
        });

        var response = await _client.GetAsync("/api/payments/stats");
        response.EnsureSuccessStatusCode();

        var stats = await response.Content.ReadFromJsonAsync<StatsResponse>(JsonOptions);

        stats!.Total.Should().Be(2);
        stats.Error.Should().BeGreaterThanOrEqualTo(1);
    }

    private sealed record CreatedResponse(long Id);
    private sealed record PaymentDetailResponse(string RawPayload);
    private sealed record PaymentItemResponse(string TransactionId, string? ContractId, string EffectiveStatus, string? ValidationError);
    private sealed record JsonElementWrapper(List<PaymentItemResponse> Items, bool HasMore);
    private sealed record StatsResponse(long Total, long Success, long Error, long Pending);
}
