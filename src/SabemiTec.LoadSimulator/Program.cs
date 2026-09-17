using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

// The API serializes camelCase (ASP.NET Core Minimal API default); this project's own
// JsonSerializer.Serialize calls use the Dictionary<string, object?> keys verbatim (already
// snake_case, the bank's own vocabulary) so this is only needed for reading the API's
// responses back, not for building outgoing payloads.
var jsonReadOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

// Stands in for the partner bank: keeps POSTing synthetic payloads at
// /webhooks/payment over real HTTP (same auth, same idempotency, same async
// processing a genuine notification would go through), so the deployed demo shows
// live movement instead of a static seed. Meant to run as its own long-lived
// process/service, independent from the API and the dashboard.

var apiUrl = Environment.GetEnvironmentVariable("API_URL") ?? "http://localhost:8080";
var webhookApiKey = Environment.GetEnvironmentVariable("WEBHOOK_API_KEY");
if (string.IsNullOrEmpty(webhookApiKey))
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
// `new Random()`) is what makes BuildPayload safe to call from multiple tasks at once. Both
// /webhooks/contracts and /webhooks/payment take the same key — this worker only ever needs
// Webhook:ApiKey, never Dashboard:ApiKey (see ApiKeyAuthMiddleware).
using var client = new HttpClient { BaseAddress = new Uri(apiUrl) };
client.DefaultRequestHeaders.Add("X-Api-Key", webhookApiKey);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
AppDomain.CurrentDomain.ProcessExit += (_, _) => cts.Cancel();

// Pulled from GET /webhooks/contracts instead of hardcoded, so this never drifts from what is
// actually seeded (migrations 0002/0003) — includes installments/total_value, not just the
// ID, so BuildPayload can post a valor consistent with the contract instead of an arbitrary
// number that visibly doesn't divide into it.
var contracts = await FetchContractsAsync(client, jsonReadOptions, cts.Token);

Console.WriteLine(
    $"Load simulator started. Target={apiUrl} Interval={intervalSeconds}s ConcurrentRequests={concurrentRequests} Contracts={contracts.Length}");

using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));

do
{
    // Fires a burst of concurrent transactions per tick instead of one at a time — closer to
    // how a partner bank actually flushes a batch, and it exercises the idempotency/locking
    // paths (ON CONFLICT, FOR UPDATE SKIP LOCKED) under real concurrency, not just in tests.
    var sends = Enumerable.Range(0, concurrentRequests).Select(_ => SendOneAsync(client, contracts, cts.Token));
    await Task.WhenAll(sends);
} while (await timer.WaitForNextTickAsync(cts.Token));

return;

// The API container may not be reachable yet on cold start (compose brings services up in
// parallel), so this retries a handful of times before giving up. If the endpoint is
// unreachable, falls back to a fixed list matching migrations 0002/0003/0004 exactly — keeps
// the worker usable and still consistent, just without the drift protection a live fetch gives.
static async Task<ContractInfo[]> FetchContractsAsync(HttpClient client, JsonSerializerOptions jsonOptions, CancellationToken ct)
{
    ContractInfo[] fallback =
    [
        new("CT-1001", "Emprestimo", 12, 6000.00m),
        new("CT-1002", "Seguro", 12, 1200.00m),
        new("CT-1003", "Emprestimo", 24, 14400.00m),
        new("CT-1004", "Seguro", 24, 2400.00m),
        new("CT-1005", "Emprestimo", 6, 3000.00m),
        new("CT-1006", "Seguro", 6, 900.00m),
        new("CT-1007", "Emprestimo", 36, 21600.00m),
        new("CT-1008", "Seguro", 12, 1800.00m),
    ];

    for (var attempt = 1; attempt <= 5; attempt++)
    {
        try
        {
            var response = await client.GetAsync("/webhooks/contracts", ct);
            if (response.IsSuccessStatusCode)
            {
                var contracts = await response.Content.ReadFromJsonAsync<ContractInfo[]>(jsonOptions, ct);
                if (contracts is { Length: > 0 })
                {
                    return contracts;
                }
            }

            Console.WriteLine($"[{DateTime.UtcNow:O}] GET /webhooks/contracts -> HTTP {(int)response.StatusCode} (tentativa {attempt}/5)");
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

static async Task SendOneAsync(HttpClient client, ContractInfo[] contracts, CancellationToken ct)
{
    var payload = BuildPayload(contracts);
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
// validation-error path (missing id_transacao, unknown status). No negative-value case —
// this is loan/insurance installment liquidation, not an account debit; the bank has no
// concept of a "withdrawal" here, so a negative valor was never a realistic payload.
static Dictionary<string, object?> BuildPayload(ContractInfo[] contracts)
{
    var random = Random.Shared;
    var contract = contracts[random.Next(contracts.Length)];
    var transactionId = $"TX-SIM-{Guid.NewGuid():N}";
    var amount = InstallmentAmount(contract);
    var paymentDate = DateTime.UtcNow.ToString("O");

    return random.NextDouble() switch
    {
        < 0.65 => Payload(transactionId, contract.ContractId, amount, paymentDate, "PAGO"),
        < 0.85 => Payload(transactionId, contract.ContractId, amount, paymentDate, "FALHA"),
        < 0.95 => PayloadWithoutTransactionId(contract.ContractId, amount, paymentDate),
        _ => Payload(transactionId, contract.ContractId, amount, paymentDate, "DESCONHECIDO"),
    };
}

// Every payment for a given contract has to add up with what the dashboard already shows
// for it (total_value / installments) — a random valor that doesn't divide into the
// contract's total looked like a bug (flagged: a 6000/12 loan showing a random R$53.46
// "installment"). Empréstimo: total_value split evenly across installments. Seguro: no
// installment plan seeded, so each event is the flat premium (total_value itself).
static decimal InstallmentAmount(ContractInfo contract) =>
    contract.TotalValue switch
    {
        { } total when contract.Installments is > 0 => Math.Round(total / contract.Installments.Value, 2),
        { } total => total,
        null => 100.00m, // no seed data for this contract id — shouldn't happen via the real endpoint
    };

static Dictionary<string, object?> Payload(
    string transactionId, string contractId, decimal amount, string paymentDate, string status) => new()
{
    ["id_transacao"] = transactionId,
    ["id_contrato"] = contractId,
    ["valor"] = amount,
    ["data_pagamento"] = paymentDate,
    ["status"] = status,
};

static Dictionary<string, object?> PayloadWithoutTransactionId(
    string contractId, decimal amount, string paymentDate) => new()
{
    ["id_contrato"] = contractId,
    ["valor"] = amount,
    ["data_pagamento"] = paymentDate,
    ["status"] = "PAGO",
};

record ContractInfo(string ContractId, string ContractType, int? Installments, decimal? TotalValue);
