using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Azure.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Sandpiper.QA.Dashboard.Server.Services;

public sealed class AdoRequestGate(IOptions<AzureDevOpsOptions> options) : IDisposable
{
    public SemaphoreSlim Semaphore { get; } = new(options.Value.MaxConcurrency);
    public void Dispose() => Semaphore.Dispose();
}

// Application-level access-token cache for AzureDevOpsClient. VisualStudioCredential on the VDI
// re-runs a multi-second broker handshake on every call instead of returning a cached token, so
// without this a single evidence request pays that cost hundreds of times. The cache holds the
// current token in memory only (never logged or persisted) and refreshes it at most once at a time
// (single-flight), so hundreds of concurrent callers that observe an expired token do not each
// trigger an independent refresh. Registered as a singleton so one token is shared across the
// transient AzureDevOpsClient instances created per request.
public sealed class AdoTokenCache(ILogger<AdoTokenCache>? logger = null) : IDisposable
{
    // Refresh slightly before expiry so a token never lapses mid-request while in use.
    private static readonly TimeSpan RefreshBuffer = TimeSpan.FromMinutes(5);
    private readonly SemaphoreSlim refreshGate = new(1, 1);
    private readonly object stateLock = new();
    private AccessToken? current;

    public async Task<AccessToken> GetTokenAsync(Func<CancellationToken, Task<AccessToken>> refresh, CancellationToken ct)
    {
        if (TryGetValid(out var cached)) return cached;
        await refreshGate.WaitAsync(ct);
        try
        {
            // Another caller may have refreshed while we waited for the single-flight gate.
            if (TryGetValid(out cached)) return cached;
            var token = await refresh(ct);
            lock (stateLock) current = token;
            logger?.LogDebug("Azure DevOps token cached; expires {ExpiresOn:O}", token.ExpiresOn);
            return token;
        }
        finally { refreshGate.Release(); }
    }

    private bool TryGetValid(out AccessToken token)
    {
        lock (stateLock)
        {
            if (current is { } value && value.ExpiresOn - RefreshBuffer > DateTimeOffset.UtcNow)
            { token = value; return true; }
        }
        token = default;
        return false;
    }

    public void Dispose() => refreshGate.Dispose();
}

public sealed class AzureDevOpsClient(HttpClient http, TokenCredential credential,
    IOptions<AzureDevOpsOptions> options, AdoRequestGate gate,
    ILogger<AzureDevOpsClient>? logger = null, AdoTokenCache? tokenCache = null)
{
    // Falls back to a per-instance cache when none is injected (unit tests); production injects the
    // shared singleton so one token serves every AzureDevOpsClient instance.
    private readonly AdoTokenCache tokenCache = tokenCache ?? new AdoTokenCache();
    public int? WorkItemId(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !uri.IsDefaultPort || uri.UserInfo.Length > 0) return null;
        var organization = options.Value.Organization;
        var sameOrganization = uri.Host.Equals("dev.azure.com", StringComparison.OrdinalIgnoreCase)
            ? Uri.UnescapeDataString(uri.Segments.ElementAtOrDefault(1)?.Trim('/') ?? "").Equals(organization, StringComparison.OrdinalIgnoreCase)
            : uri.Host.Equals($"{organization}.visualstudio.com", StringComparison.OrdinalIgnoreCase);
        if (!sameOrganization) return null;
        var match = System.Text.RegularExpressions.Regex.Match(uri.AbsolutePath, @"/_apis/wit/workitems/(\d+)/?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success && int.TryParse(match.Groups[1].Value, out var id) && id > 0 ? id : null;
    }
    public async Task<string> ScopeKeyAsync(CancellationToken ct)
    {
        var token = await AcquireTokenAsync(ct);
        // Cache isolation without logging, returning, decoding or persisting an access token.
        return options.Value.BaseUrl + ":" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token.Token)));
    }
    private static readonly TokenRequestContext TokenContext =
        new(["499b84ac-1321-427f-aa17-267ca6975798/.default"]);

    // Returns a cached access token, acquiring a new one only when the cache is empty or near expiry.
    // The bounded-timeout acquisition, scope, cancellation handling and logging are preserved in the
    // refresh delegate; the cache guarantees at most one in-flight acquisition at a time.
    private Task<AccessToken> AcquireTokenAsync(CancellationToken ct) =>
        tokenCache.GetTokenAsync(RefreshTokenAsync, ct);

    // Acquires a fresh access token under a bounded timeout so a stalled credential handshake fails the
    // request explicitly instead of hanging. Only acquisition duration (never the token) is logged.
    private async Task<AccessToken> RefreshTokenAsync(CancellationToken ct)
    {
        var timeout = TimeSpan.FromSeconds(Math.Max(1, options.Value.TokenTimeoutSeconds));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(timeout);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var token = await credential.GetTokenAsync(TokenContext, linked.Token);
            logger?.LogDebug("Azure DevOps token acquired in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            return token;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger?.LogWarning("Azure DevOps token acquisition timed out after {ElapsedMs}ms; configured limit {TimeoutSeconds}s",
                stopwatch.ElapsedMilliseconds, timeout.TotalSeconds);
            throw new TimeoutException(
                $"Azure DevOps sign-in/token acquisition did not complete within the configured {timeout.TotalSeconds:N0}s limit. Check local Visual Studio ADO sign-in.");
        }
    }

    private async Task<(List<JsonElement> Items, string? Token)> PageAsync(
        string path, Dictionary<string, string> query, CancellationToken ct, bool organizationScope = false)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.Organization) || string.IsNullOrWhiteSpace(settings.Project))
            throw new ArgumentException("Configure AzureDevOps organization and project.");
        var baseUrl = organizationScope ? settings.OrganizationBaseUrl : settings.BaseUrl;
        var url = $"{baseUrl}/_apis/{path}?api-version=7.1";
        foreach (var pair in query) url += $"&{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}";
        var gateWait = System.Diagnostics.Stopwatch.StartNew();
        await gate.Semaphore.WaitAsync(ct);
        gateWait.Stop();
        var callElapsed = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            for (var attempt = 0; ; attempt++)
            {
                var token = await AcquireTokenAsync(ct);
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                using var response = await SendWithDiagnosticsAsync(request, path, attempt, ct);
                if ((response.StatusCode == HttpStatusCode.TooManyRequests ||
                     response.StatusCode == HttpStatusCode.ServiceUnavailable) && attempt < 3)
                {
                    var wait = response.Headers.RetryAfter?.Delta ??
                        (response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow) ??
                        TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    if (wait > TimeSpan.FromSeconds(60))
                        throw new HttpRequestException("ADO throttling requires a later retry.", null, response.StatusCode);
                    logger?.LogInformation("Azure DevOps throttled _apis/{Path}: status {Status}; retry {Attempt} after {WaitMs}ms",
                        path, (int)response.StatusCode, attempt + 1, Math.Max(0, wait.TotalMilliseconds));
                    await Task.Delay(wait < TimeSpan.Zero ? TimeSpan.Zero : wait, ct);
                    continue;
                }
                if (!response.IsSuccessStatusCode)
                {
                    // Capture the exact endpoint, status and a bounded, non-sensitive error-body snippet
                    // (ADO error bodies carry a descriptive message, not credentials) before throwing.
                    var contentType = response.Content.Headers.ContentType?.MediaType;
                    var body = await response.Content.ReadAsStringAsync(ct);
                    logger?.LogWarning("Azure DevOps GET _apis/{Path} returned {Status} {Reason}; contentType {ContentType}; call {CallMs}ms; body {Body}",
                        path, (int)response.StatusCode, response.ReasonPhrase, contentType, callElapsed.ElapsedMilliseconds,
                        body.Length > 300 ? body[..300] : body);
                }
                response.EnsureSuccessStatusCode();
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                if (!json.RootElement.TryGetProperty("value", out var values) || values.ValueKind != JsonValueKind.Array)
                    throw new InvalidDataException("Invalid Azure DevOps list response.");
                var items = values.EnumerateArray().Select(item => item.Clone()).ToList();
                var continuation = response.Headers.TryGetValues("x-ms-continuationtoken", out var headers)
                    ? headers.FirstOrDefault() : null;
                logger?.LogDebug("Azure DevOps GET _apis/{Path}: status {Status}; items {Items}; retries {Retries}; " +
                    "semaphoreWait {GateWaitMs}ms; call {CallMs}ms; continued {Continued}",
                    path, (int)response.StatusCode, items.Count, attempt, gateWait.ElapsedMilliseconds,
                    callElapsed.ElapsedMilliseconds, continuation != null);
                return (items, continuation);
            }
        }
        finally { gate.Semaphore.Release(); }
    }

    // Distinguishes a caller/client-initiated cancellation (expected: scope change, navigation,
    // refresh or request abort) from an HttpClient.Timeout that elapsed with no HTTP response.
    // Caller cancellations propagate unchanged so the pipeline can end them quietly. A timeout is
    // surfaced as an explicit TimeoutException (not an ambiguous cancellation) so it is never
    // misreported as a generic Azure DevOps failure. Diagnostics are structured and non-sensitive:
    // no tokens, authorization headers, query values or payloads are logged.
    private async Task<HttpResponseMessage> SendWithDiagnosticsAsync(
        HttpRequestMessage request, string path, int attempt, CancellationToken ct)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            return await http.SendAsync(request, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            logger?.LogDebug("Azure DevOps request cancelled by caller before completion: {Method} _apis/{Path}; " +
                "attempt {Attempt}; elapsed {ElapsedMs}ms; callerCancelled=true; timeoutRelated=false",
                request.Method.Method, path, attempt + 1, stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (OperationCanceledException timeout)
        {
            logger?.LogWarning(timeout, "Azure DevOps request timed out before any HTTP status: {Method} _apis/{Path}; " +
                "attempt {Attempt}; configured HTTP timeout {TimeoutSeconds:N0}s; elapsed {ElapsedMs}ms; " +
                "callerCancelled=false; timeoutRelated=true",
                request.Method.Method, path, attempt + 1, http.Timeout.TotalSeconds, stopwatch.ElapsedMilliseconds);
            throw new TimeoutException(
                $"Azure DevOps did not respond to '{path}' within the configured {http.Timeout.TotalSeconds:N0}s HTTP timeout.", timeout);
        }
        catch (HttpRequestException connection)
        {
            // No HTTP response was received (DNS, TLS, proxy, connection reset). Status is null here.
            logger?.LogWarning("Azure DevOps request failed before any HTTP response: _apis/{Path}; attempt {Attempt}; " +
                "httpError {HttpError}; inner {Inner}; elapsed {ElapsedMs}ms",
                path, attempt + 1, connection.HttpRequestError, connection.InnerException?.GetType().Name, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    public async Task<List<JsonElement>> ListAsync(string path, CancellationToken ct, Dictionary<string, string>? parameters = null, bool organizationScope = false)
    {
        List<JsonElement> all = [];
        HashSet<string> seen = [];
        string? token = null;
        for (var page = 0; page < 10000; page++)
        {
            var query = parameters == null ? new Dictionary<string, string>() : new Dictionary<string, string>(parameters);
            if (token != null) query["continuationToken"] = token;
            var result = await PageAsync(path, query, ct, organizationScope);
            all.AddRange(result.Items);
            token = result.Token;
            if (string.IsNullOrEmpty(token)) return all;
            if (!seen.Add(token)) throw new InvalidDataException("Repeated ADO continuation token.");
        }
        throw new InvalidDataException("ADO pagination limit reached; totals withheld.");
    }

    public async Task<List<JsonElement>> HistoryAsync(string path,
        Dictionary<string, string> parameters, CancellationToken ct, string identityField = "id")
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
                var id = Number(item, identityField);
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
