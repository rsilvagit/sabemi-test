using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace SabemiTec.Tests.Integration;

/// <summary>Proves the two mechanisms compose without interference: a 429 never persists
/// anything, so the retry it provokes still goes through the normal idempotent path.</summary>
[Collection(DatabaseCollection.Name)]
public class RateLimitingTests(DatabaseFixture db) : IAsyncLifetime
{
    public async Task InitializeAsync() => await db.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Burst_TriggersRateLimit_AndRejectedRequestRetriesIdempotently()
    {
        await using var factory = new SabemiWebApplicationFactory(
            db.ConnectionString, rateLimitTokenLimit: 3, rateLimitTokensPerPeriod: 3);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", SabemiWebApplicationFactory.TestApiKey);

        var attempts = new List<(string TransactionId, HttpResponseMessage Response)>();
        for (var i = 0; i < 10; i++)
        {
            var transactionId = $"TX-RATE-{i}";
            var response = await client.PostAsJsonAsync("/webhooks/pagamento", new
            {
                id_transacao = transactionId,
                id_contrato = "CT-RATE",
                valor = 10m,
                data_pagamento = "2026-09-15T10:00:00Z",
                status = "PAGO"
            });
            attempts.Add((transactionId, response));
        }

        var rejected = attempts.Where(a => a.Response.StatusCode == HttpStatusCode.TooManyRequests).ToList();
        rejected.Should().NotBeEmpty("a burst of 10 against a bucket of 3 tokens must trip the limiter");
        rejected[0].Response.Headers.RetryAfter.Should().NotBeNull("429 must tell the caller when to retry");

        var retryTransactionId = rejected[0].TransactionId;
        var retryPayload = new
        {
            id_transacao = retryTransactionId,
            id_contrato = "CT-RATE",
            valor = 10m,
            data_pagamento = "2026-09-15T10:00:00Z",
            status = "PAGO"
        };

        HttpResponseMessage? retryResponse = null;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            retryResponse = await client.PostAsJsonAsync("/webhooks/pagamento", retryPayload);
            if (retryResponse.StatusCode != HttpStatusCode.TooManyRequests)
            {
                break;
            }
            await Task.Delay(200);
        }

        retryResponse!.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "the rejected transaction was never persisted, so its retry is a fresh, valid POST");

        await using var conn = new NpgsqlConnection(db.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "select count(*) from payment_event where transaction_id = @tx", conn);
        cmd.Parameters.AddWithValue("tx", retryTransactionId);
        var count = (long)(await cmd.ExecuteScalarAsync())!;

        count.Should().Be(1, "429 must never leave a ghost row — idempotency still holds after the retry");
    }
}
