using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace SabemiTec.Tests.Integration;

/// <summary>
/// Proves the webhook and dashboard keys are genuinely independent: a leak of one must never
/// grant access to the other's routes.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ApiKeyAuthTests(DatabaseFixture db) : IAsyncLifetime
{
    private const string WebhookApiKey = "test-webhook-key";
    private const string DashboardApiKey = "test-dashboard-key";

    private SabemiWebApplicationFactory _factory = null!;

    public async Task InitializeAsync()
    {
        await db.ResetAsync();
        _factory = new SabemiWebApplicationFactory(
            db.ConnectionString, webhookApiKey: WebhookApiKey, dashboardApiKey: DashboardApiKey);
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task DashboardKey_IsRejected_OnWebhookRoute()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", DashboardApiKey);

        var response = await client.PostAsJsonAsync("/webhooks/payment", new
        {
            id_transacao = "TX-CROSS-KEY-001",
            id_contrato = "CT-CROSS",
            valor = 10m,
            data_pagamento = "2026-09-15T10:00:00Z",
            status = "PAGO"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "the dashboard's key must never authorize the webhook the partner bank uses");
    }

    [Fact]
    public async Task WebhookKey_IsRejected_OnDashboardRoute()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", WebhookApiKey);

        var response = await client.GetAsync("/api/payments/stats");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "the partner bank's webhook key must never authorize the dashboard's read API");
    }
}
