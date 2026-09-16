using System.Text;
using System.Text.Json;

// Stands in for the partner bank: keeps POSTing synthetic payloads at
// /webhooks/payment over real HTTP (same auth, same idempotency, same async
// processing a genuine notification would go through), so the deployed demo shows
// live movement instead of a static seed. Meant to run as its own long-lived
// process/service, independent from the API and the dashboard.

var apiUrl = Environment.GetEnvironmentVariable("API_URL") ?? "http://localhost:8080";
var apiKey = Environment.GetEnvironmentVariable("WEBHOOK_API_KEY")
    ?? throw new InvalidOperationException("WEBHOOK_API_KEY is required.");
var intervalSeconds = int.TryParse(Environment.GetEnvironmentVariable("INTERVAL_SECONDS"), out var parsed)
    ? parsed
    : 5;

var contractIds = new[] { "CT-1001", "CT-1002", "CT-1003", "CT-1004", "CT-1005", "CT-1006", "CT-1007", "CT-1008" };
var random = new Random();

using var client = new HttpClient { BaseAddress = new Uri(apiUrl) };
client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
AppDomain.CurrentDomain.ProcessExit += (_, _) => cts.Cancel();

Console.WriteLine($"Load simulator started. Target={apiUrl} Interval={intervalSeconds}s");

using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));

do
{
    try
    {
        await SendOneAsync(client, contractIds, random, cts.Token);
    }
    catch (OperationCanceledException) when (cts.IsCancellationRequested)
    {
        break;
    }
    catch (Exception ex)
    {
        // Keep the loop alive across transient failures (cold start, network blip)
        // instead of letting one bad request kill the process.
        Console.WriteLine($"[{DateTime.UtcNow:O}] erro ao enviar: {ex.Message}");
    }
} while (await timer.WaitForNextTickAsync(cts.Token));

return;

static async Task SendOneAsync(HttpClient client, string[] contractIds, Random random, CancellationToken ct)
{
    var payload = BuildPayload(contractIds, random);
    var json = JsonSerializer.Serialize(payload);

    using var content = new StringContent(json, Encoding.UTF8, "application/json");
    using var response = await client.PostAsync("/webhooks/payment", content, ct);

    Console.WriteLine(
        $"[{DateTime.UtcNow:O}] {payload.GetValueOrDefault("id_transacao") ?? "(sem id_transacao)"} -> HTTP {(int)response.StatusCode}");
}

// Weighted so the dashboard keeps showing a realistic mix of outcomes: mostly paid,
// some bank-side failures, and a slice of malformed payloads that exercise the
// validation-error path (missing id_transacao, negative value, unknown status).
static Dictionary<string, object?> BuildPayload(string[] contractIds, Random random)
{
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
