using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Azure.Core;
using Microsoft.Extensions.Options;

namespace Sandpiper.QA.Dashboard.Server.Services;

public sealed class AdoRequestGate(IOptions<AzureDevOpsOptions> options) : IDisposable
{
    public SemaphoreSlim Semaphore { get; } = new(options.Value.MaxConcurrency);
    public void Dispose() => Semaphore.Dispose();
}

public sealed class AzureDevOpsClient(HttpClient http, TokenCredential credential,
    IOptions<AzureDevOpsOptions> options, AdoRequestGate gate)
{
    private static readonly TokenRequestContext TokenContext =
        new(["499b84ac-1321-427f-aa17-267ca6975798/.default"]);

    private async Task<(List<JsonElement> Items, string? Token)> PageAsync(
        string path, Dictionary<string, string> query, CancellationToken ct)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.Organization) || string.IsNullOrWhiteSpace(settings.Project))
            throw new ArgumentException("Configure AzureDevOps organization and project.");
        var url = $"{settings.BaseUrl}/_apis/{path}?api-version=7.1";
        foreach (var pair in query) url += $"&{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}";
        await gate.Semaphore.WaitAsync(ct);
        try
        {
            for (var attempt = 0; ; attempt++)
            {
                var token = await credential.GetTokenAsync(TokenContext, ct);
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                using var response = await http.SendAsync(request, ct);
                if ((response.StatusCode == HttpStatusCode.TooManyRequests ||
                     response.StatusCode == HttpStatusCode.ServiceUnavailable) && attempt < 3)
                {
                    var wait = response.Headers.RetryAfter?.Delta ??
                        (response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow) ??
                        TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    if (wait > TimeSpan.FromSeconds(60))
                        throw new HttpRequestException("ADO throttling requires a later retry.", null, response.StatusCode);
                    await Task.Delay(wait < TimeSpan.Zero ? TimeSpan.Zero : wait, ct);
                    continue;
                }
                response.EnsureSuccessStatusCode();
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                if (!json.RootElement.TryGetProperty("value", out var values) || values.ValueKind != JsonValueKind.Array)
                    throw new InvalidDataException("Invalid Azure DevOps list response.");
                var items = values.EnumerateArray().Select(item => item.Clone()).ToList();
                var continuation = response.Headers.TryGetValues("x-ms-continuationtoken", out var headers)
                    ? headers.FirstOrDefault() : null;
                return (items, continuation);
            }
        }
        finally { gate.Semaphore.Release(); }
    }

    public async Task<List<JsonElement>> ListAsync(string path, CancellationToken ct)
    {
        List<JsonElement> all = [];
        HashSet<string> seen = [];
        string? token = null;
        for (var page = 0; page < 10000; page++)
        {
            var query = new Dictionary<string, string>();
            if (token != null) query["continuationToken"] = token;
            var result = await PageAsync(path, query, ct);
            all.AddRange(result.Items);
            token = result.Token;
            if (string.IsNullOrEmpty(token)) return all;
            if (!seen.Add(token)) throw new InvalidDataException("Repeated ADO continuation token.");
        }
        throw new InvalidDataException("ADO pagination limit reached; totals withheld.");
    }

    public async Task<List<JsonElement>> HistoryAsync(string path,
        Dictionary<string, string> parameters, CancellationToken ct)
    {
        Dictionary<int, JsonElement> all = [];
        var skip = 0;
        for (var page = 0; page < 10000; page++)
        {
            var query = new Dictionary<string, string>(parameters) { ["$skip"] = skip.ToString(), ["$top"] = "200" };
            var result = await PageAsync(path, query, ct);
            if (result.Items.Count == 0) return all.Values.ToList();
            var previous = all.Count;
            foreach (var item in result.Items)
            {
                var id = Number(item, "id");
                if (id <= 0) throw new InvalidDataException("Invalid history identity.");
                if (all.TryGetValue(id, out var existing) && existing.GetRawText() != item.GetRawText())
                    throw new InvalidDataException("History changed during pagination; refresh.");
                all[id] = item;
            }
            if (all.Count == previous) throw new InvalidDataException("History pagination did not advance.");
            skip += result.Items.Count;
        }
        throw new InvalidDataException("History pagination limit reached; totals withheld.");
    }

    public static int Number(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && int.TryParse(value.ToString(), out var id) ? id : 0;

    public static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
