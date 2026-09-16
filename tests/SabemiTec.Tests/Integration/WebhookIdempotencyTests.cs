using System.Net;
using System.Text;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace SabemiTec.Tests.Integration;

/// <summary>
/// The central test of the challenge: proves idempotency survives real concurrency, not
/// just sequential calls. A SELECT-before-INSERT approach would fail here.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class WebhookIdempotencyTests(DatabaseFixture db) : IAsyncLifetime
{
    private SabemiWebApplicationFactory _factory = null!;

    public async Task InitializeAsync()
    {
        await db.ResetAsync();
        _factory = new SabemiWebApplicationFactory(db.ConnectionString);
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task ConcurrentPostsWithSameTransactionId_ResultInASingleRow()
    {
        const string transactionId = "TX-CONCURRENT-001";
        const int concurrentRequests = 20;

        var payload = new
        {
            id_transacao = transactionId,
            id_contrato = "CT-CONCURRENT",
            valor = 100.00m,
            data_pagamento = "2026-09-15T10:00:00Z",
            status = "PAGO"
        };

        var tasks = Enumerable.Range(0, concurrentRequests).Select(async _ =>
        {
            using var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Add("X-Api-Key", SabemiWebApplicationFactory.TestApiKey);
            var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            return await client.PostAsync("/webhooks/payment", content);
        });

        var responses = await Task.WhenAll(tasks);

        responses.Count(r => r.StatusCode == HttpStatusCode.Accepted).Should().Be(1,
            "only one request should have created the event");
        responses.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(concurrentRequests - 1,
            "the rest should be recognized as a duplicate, never as an error");
        responses.Should().OnlyContain(
            r => r.StatusCode == HttpStatusCode.Accepted || r.StatusCode == HttpStatusCode.OK,
            "no concurrent request should result in a 500");

        await using var conn = new NpgsqlConnection(db.ConnectionString);
        await conn.OpenAsync();

        await using var countCmd = new NpgsqlCommand(
            "select count(*) from payment_event where transaction_id = @tx", conn);
        countCmd.Parameters.AddWithValue("tx", transactionId);
        var count = (long)(await countCmd.ExecuteScalarAsync())!;

        count.Should().Be(1, "the unique constraint must guarantee exactly one row, even under concurrency");
    }
}
