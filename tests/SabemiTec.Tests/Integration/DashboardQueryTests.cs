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
    public async Task InvalidPayload_ExcludedFromMainList_ButVisibleInInvalidEndpoint()
    {
        await _client.PostAsJsonAsync("/webhooks/payment", new
        {
            id_transacao = "TX-DASH-001",
            id_contrato = "CT-DASH-01",
            valor = -10,
            data_pagamento = "2026-09-15T10:00:00Z",
            status = "PAGO"
        });

        // No dependable transaction/contract reference on a malformed payload — excluded
        // from the main list entirely, not shown as an "Error" row next to real payments.
        var mainList = await _client.GetAsync("/api/payments?status=Error");
        mainList.EnsureSuccessStatusCode();
        var mainBody = await mainList.Content.ReadFromJsonAsync<JsonElementWrapper>(JsonOptions);
        mainBody!.Items.Should().NotContain(i => i.TransactionId == "TX-DASH-001");

        // Still visible — just in its own endpoint, not implying a trustworthy contract link.
        var invalidList = await _client.GetAsync("/api/payments/invalid");
        invalidList.EnsureSuccessStatusCode();
        var invalidBody = await invalidList.Content.ReadFromJsonAsync<InvalidListResponse>(JsonOptions);

        invalidBody!.Items.Should().ContainSingle(i => i.TransactionId == "TX-DASH-001");
        var item = invalidBody.Items.Single(i => i.TransactionId == "TX-DASH-001");
        item.ValidationError.Should().Contain("valor");
    }

    [Fact]
    public async Task FilterByContractId_ReturnsOnlyMatchingContract()
    {
        await _client.PostAsJsonAsync("/webhooks/payment", new
        {
            id_transacao = "TX-DASH-010",
            id_contrato = "CT-A",
            valor = 10,
            data_pagamento = "2026-09-15T10:00:00Z",
            status = "PAGO"
        });
        await _client.PostAsJsonAsync("/webhooks/payment", new
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
            await _client.PostAsJsonAsync("/webhooks/payment", new
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
        var post = await _client.PostAsJsonAsync("/webhooks/payment", new
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
        await _client.PostAsJsonAsync("/webhooks/payment", new
        {
            id_transacao = "TX-STATS-OK",
            id_contrato = "CT-STATS",
            valor = 10,
            data_pagamento = "2026-09-15T10:00:00Z",
            status = "PAGO"
        });
        await _client.PostAsJsonAsync("/webhooks/payment", new
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

        // The invalid payload (valor < 0) doesn't count toward the main stats anymore — it's
        // excluded from the list those cards summarize, same as /api/payments. Total (not
        // Success) is what's timing-independent here — the valid one may still be Pending.
        stats!.Total.Should().Be(1);

        var invalidResponse = await _client.GetAsync("/api/payments/invalid");
        var invalid = await invalidResponse.Content.ReadFromJsonAsync<InvalidListResponse>(JsonOptions);
        invalid!.Total.Should().Be(1);
    }

    private sealed record CreatedResponse(long Id);
    private sealed record PaymentDetailResponse(string RawPayload);
    private sealed record PaymentItemResponse(string TransactionId, string? ContractId, string EffectiveStatus, string? ValidationError);
    private sealed record JsonElementWrapper(List<PaymentItemResponse> Items, bool HasMore);
    private sealed record StatsResponse(long Total, long Success, long Error, long Pending);
    private sealed record InvalidItemResponse(string TransactionId, string? ContractId, string? ValidationError);
    private sealed record InvalidListResponse(List<InvalidItemResponse> Items, bool HasMore, long Total);
}
