using System.Net.Http.Json;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace SabemiTec.Tests.Integration;

/// <summary>Exercises the worker end to end — never with Thread.Sleep: active polling with a timeout.</summary>
[Collection(DatabaseCollection.Name)]
public class PaymentProcessingTests(DatabaseFixture db) : IAsyncLifetime
{
    private SabemiWebApplicationFactory _factory = null!;

    public async Task InitializeAsync()
    {
        await db.ResetAsync();
        _factory = new SabemiWebApplicationFactory(db.ConnectionString, workerEnabled: true, simulatedDelayMs: 0);
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task FullCycle_EventEndsUpProcessedAndContractUpdated()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", SabemiWebApplicationFactory.TestApiKey);

        var payload = new
        {
            id_transacao = "TX-WORKER-001",
            id_contrato = "CT-WORKER-01",
            valor = 250.00m,
            data_pagamento = "2026-09-15T10:00:00Z",
            status = "PAGO"
        };

        var response = await client.PostAsJsonAsync("/webhooks/pagamento", payload);
        response.EnsureSuccessStatusCode();

        var processingStatus = await PollProcessingStatusAsync("TX-WORKER-001", TimeSpan.FromSeconds(10));
        processingStatus.Should().Be(2, "the worker must mark the event as Processed");

        await using var conn = new NpgsqlConnection(db.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "select total_paid, payments_count from contract_status where contract_id = @c", conn);
        cmd.Parameters.AddWithValue("c", "CT-WORKER-01");
        await using var reader = await cmd.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();
        reader.GetDecimal(0).Should().Be(250.00m);
        reader.GetInt32(1).Should().Be(1);
    }

    [Fact]
    public async Task MultiplePaymentsOnSameContract_AccumulateCorrectly()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", SabemiWebApplicationFactory.TestApiKey);

        await client.PostAsJsonAsync("/webhooks/pagamento", new
        {
            id_transacao = "TX-MULTI-001",
            id_contrato = "CT-MULTI-01",
            valor = 100.00m,
            data_pagamento = "2026-09-10T10:00:00Z",
            status = "PAGO"
        });
        await client.PostAsJsonAsync("/webhooks/pagamento", new
        {
            id_transacao = "TX-MULTI-002",
            id_contrato = "CT-MULTI-01",
            valor = 50.00m,
            data_pagamento = "2026-09-12T10:00:00Z",
            status = "FALHA"
        });
        await client.PostAsJsonAsync("/webhooks/pagamento", new
        {
            id_transacao = "TX-MULTI-003",
            id_contrato = "CT-MULTI-01",
            valor = 75.00m,
            data_pagamento = "2026-09-14T10:00:00Z",
            status = "PAGO"
        });

        await PollProcessingStatusAsync("TX-MULTI-001", TimeSpan.FromSeconds(10));
        await PollProcessingStatusAsync("TX-MULTI-002", TimeSpan.FromSeconds(10));
        await PollProcessingStatusAsync("TX-MULTI-003", TimeSpan.FromSeconds(10));

        await using var conn = new NpgsqlConnection(db.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            """
            select total_paid, payments_count, failed_count, last_transaction_id
            from contract_status where contract_id = @c
            """, conn);
        cmd.Parameters.AddWithValue("c", "CT-MULTI-01");
        await using var reader = await cmd.ExecuteReaderAsync();
        (await reader.ReadAsync()).Should().BeTrue();

        reader.GetDecimal(0).Should().Be(175.00m, "only successful (PAGO) payments add to the total");
        reader.GetInt32(1).Should().Be(2, "two successful payments");
        reader.GetInt32(2).Should().Be(1, "one failed payment");
        reader.GetString(3).Should().Be("TX-MULTI-003", "the most recent payment by date");
    }

    private async Task<short> PollProcessingStatusAsync(string transactionId, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow.Add(timeout);

        while (DateTime.UtcNow < deadline)
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                "select processing_status from payment_event where transaction_id = @tx", conn);
            cmd.Parameters.AddWithValue("tx", transactionId);
            var result = await cmd.ExecuteScalarAsync();

            if (result is short status && status is 2 or 3)
            {
                return status;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException($"Event {transactionId} did not finish processing in time.");
    }
}
