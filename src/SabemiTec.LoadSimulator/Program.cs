using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

// Stands in for the partner bank: keeps POSTing synthetic payloads at
// /webhooks/payment over real HTTP (same auth, same idempotency, same async
// processing a genuine notification would go through), so the deployed demo shows
// live movement instead of a static seed. Meant to run as its own long-lived
// process/service, independent from the API and the dashboard.

var apiUrl = Environment.GetEnvironmentVariable("API_URL") ?? "http://localhost:8080";
var apiKey = Environment.GetEnvironmentVariable("WEBHOOK_API_KEY");
if (string.IsNullOrEmpty(apiKey))
{
    throw new InvalidOperationException("WEBHOOK_API_KEY is required.");
}
var intervalSeconds = int.TryParse(Environment.GetEnvironmentVariable("INTERVAL_SECONDS"), out var parsed)
    ? parsed
    : 5;
var concurrentRequests = int.TryParse(Environment.GetEnvironmentVariable("CONCURRENT_REQUESTS"), out var concurrency)
    ? Math.Max(1, concurrency)
    : 5;

// A single HttpClient is thread-safe for concurrent requests by design; Random.Shared (not
// `new Random()`) is what makes BuildPayload safe to call from multiple tasks at once.
using var client = new HttpClient { BaseAddress = new Uri(apiUrl) };
client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
AppDomain.CurrentDomain.ProcessExit += (_, _) => cts.Cancel();

// Pulled from GET /api/payments/contracts instead of hardcoded, so this never drifts from
// what is actually seeded (migration 0002) — a hardcoded duplicate list here would silently
// go stale the moment the seed changes.
var contractIds = await FetchContractIdsAsync(client, cts.Token);

Console.WriteLine(
    $"Load simulator started. Target={apiUrl} Interval={intervalSeconds}s ConcurrentRequests={concurrentRequests} Contracts={contractIds.Length}");

using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));

do
{
    // Fires a burst of concurrent transactions per tick instead of one at a time — closer to
    // how a partner bank actually flushes a batch, and it exercises the idempotency/locking
    // paths (ON CONFLICT, FOR UPDATE SKIP LOCKED) under real concurrency, not just in tests.
    var sends = Enumerable.Range(0, concurrentRequests).Select(_ => SendOneAsync(client, contractIds, cts.Token));
    await Task.WhenAll(sends);
} while (await timer.WaitForNextTickAsync(cts.Token));

return;

// The API container may not be reachable yet on cold start (compose brings services up in
// parallel), so this retries a handful of times before giving up. If the endpoint is
// unreachable or returns no contracts (e.g. an older API without migration 0002 applied),
// falls back to the same fixed list the seed uses today — keeps the worker usable, just
// without the drift protection.
static async Task<string[]> FetchContractIdsAsync(HttpClient client, CancellationToken ct)
{
    string[] fallback = ["CT-1001", "CT-1002", "CT-1003", "CT-1004", "CT-1005", "CT-1006", "CT-1007", "CT-1008"];

    for (var attempt = 1; attempt <= 5; attempt++)
    {
        try
        {
            var response = await client.GetAsync("/api/payments/contracts", ct);
            if (response.IsSuccessStatusCode)
            {
                var contractIds = await response.Content.ReadFromJsonAsync<string[]>(cancellationToken: ct);
                if (contractIds is { Length: > 0 })
                {
                    return contractIds;
                }
            }

            Console.WriteLine($"[{DateTime.UtcNow:O}] GET /api/payments/contracts -> HTTP {(int)response.StatusCode} (tentativa {attempt}/5)");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.WriteLine($"[{DateTime.UtcNow:O}] falha ao buscar contratos (tentativa {attempt}/5): {ex.Message}");
        }

        await Task.Delay(TimeSpan.FromSeconds(3), ct);
    }

    Console.WriteLine($"[{DateTime.UtcNow:O}] usando lista de contratos fixa de fallback ({fallback.Length} contratos).");
    return fallback;
}

static async Task SendOneAsync(HttpClient client, string[] contractIds, CancellationToken ct)
{
    var payload = BuildPayload(contractIds);
    var json = JsonSerializer.Serialize(payload);

    try
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/webhooks/payment", content, ct);

        Console.WriteLine(
            $"[{DateTime.UtcNow:O}] {payload.GetValueOrDefault("id_transacao") ?? "(sem id_transacao)"} -> HTTP {(int)response.StatusCode}");
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested)
    {
        // shutting down, nothing to report
    }
    catch (Exception ex)
    {
        // Keep the loop alive across transient failures (cold start, network blip) instead of
        // one bad request in the burst taking down the whole batch via Task.WhenAll.
        Console.WriteLine($"[{DateTime.UtcNow:O}] erro ao enviar: {ex.Message}");
    }
}

// Weighted so the dashboard keeps showing a realistic mix of outcomes: mostly paid,
// some bank-side failures, and a slice of malformed payloads that exercise the
// validation-error path (missing id_transacao, negative value, unknown status).
static Dictionary<string, object?> BuildPayload(string[] contractIds)
{
    var random = Random.Shared;
    var contractId = contractIds[random.Next(contractIds.Length)];
    var transactionId = $"TX-SIM-{Guid.NewGuid():N}";
    var amount = Math.Round(random.NextDouble() * 990 + 10, 2);
    var paymentDate = DateTime.UtcNow.ToString("O");

    return random.NextDouble() switch
    {
        < 0.65 => Payload(transactionId, contractId, amount, paymentDate, "PAGO"),
        < 0.85 => Payload(transactionId, contractId, amount, paymentDate, "FALHA"),
        < 0.92 => PayloadWithoutTransactionId(contractId, amount, paymentDate),
        < 0.97 => Payload(transactionId, contractId, -amount, paymentDate, "PAGO"),
        _ => Payload(transactionId, contractId, amount, paymentDate, "DESCONHECIDO"),
    };
}

static Dictionary<string, object?> Payload(
    string transactionId, string contractId, double amount, string paymentDate, string status) => new()
{
    ["id_transacao"] = transactionId,
    ["id_contrato"] = contractId,
    ["valor"] = amount,
    ["data_pagamento"] = paymentDate,
    ["status"] = status,
};

static Dictionary<string, object?> PayloadWithoutTransactionId(
    string contractId, double amount, string paymentDate) => new()
{
    ["id_contrato"] = contractId,
    ["valor"] = amount,
    ["data_pagamento"] = paymentDate,
    ["status"] = "PAGO",
};
