using System.Globalization;
using System.Net;
using Azure.Core;
using Azure.Identity;
using Sandpiper.QA.Dashboard.Server.Services;
using Sandpiper.QA.Dashboard.Server.Services.Analytics;
using Sandpiper.QA.Dashboard.Server.Services.Reporting;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true).AddEnvironmentVariables();
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddOptions<AzureDevOpsOptions>()
    .Bind(builder.Configuration.GetSection("AzureDevOps"))
    .Validate(options => options.MaxConcurrency is >= 1 and <= 8, "Concurrency must be between 1 and 8.")
    .Validate(options => TimeOnly.TryParseExact(options.DsrCutoff, "HH:mm", CultureInfo.InvariantCulture,
        DateTimeStyles.None, out _), "DSR cutoff must use HH:mm.")
    .ValidateOnStart();
builder.Services.AddSingleton<TokenCredential>(_ => new VisualStudioCredential(new VisualStudioCredentialOptions
{
    TenantId = builder.Configuration["AzureDevOps:TenantId"]
}));
builder.Services.AddSingleton<AdoRequestGate>();
builder.Services.AddHttpClient<AzureDevOpsClient>(client => client.Timeout = TimeSpan.FromSeconds(60))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddHttpClient<AzureDevOpsService>();
builder.Services.AddTransient<FrontierAnalyticsService>();
builder.Services.AddTransient<ExecutionHistoryService>();
builder.Services.AddSingleton<ReportStore>();
builder.Services.AddTransient<ReportService>();
var origins = builder.Configuration.GetSection("Dashboard:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("DashboardClient", policy =>
{
    if (origins.Length > 0) policy.WithOrigins(origins).WithHeaders("X-Sandpiper-Client", "Content-Type").WithMethods("GET", "POST");
}));

var app = builder.Build();
// Until enterprise caller authentication is approved, this is a loopback-only developer API.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api") && context.Request.Path != "/api/health")
    {
        var origin = context.Request.Headers.Origin.ToString();
        var localHost = context.Request.Host.Host is "localhost" or "127.0.0.1" or "[::1]" or "::1";
        if (!app.Environment.IsDevelopment())
        {
            context.Response.StatusCode = 503;
            await context.Response.WriteAsJsonAsync(new { error = "Enterprise API authentication is not configured." });
            return;
        }
        if (!localHost || context.Connection.RemoteIpAddress is not { } address || !IPAddress.IsLoopback(address) ||
            (origin.Length > 0 && !origins.Contains(origin, StringComparer.OrdinalIgnoreCase)))
        {
            context.Response.StatusCode = 403;
            return;
        }
        if (context.Request.Method != "OPTIONS" && context.Request.Headers["X-Sandpiper-Client"] != "dashboard")
        {
            context.Response.StatusCode = 403;
            return;
        }
    }
    try { await next(context); }
    catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
    catch (Exception error)
    {
        var status = error switch
        {
            ArgumentException => 400,
            CredentialUnavailableException or AuthenticationFailedException => 503,
            HttpRequestException or InvalidDataException or System.Text.Json.JsonException => 502,
            OperationCanceledException => 504,
            _ => 500
        };
        app.Logger.LogWarning("Analytics request failed: {Type}; trace {TraceId}", error.GetType().Name, context.TraceIdentifier);
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new { error = status == 400
            ? "Invalid reporting scope or configuration. Check plan, suite and offset-aware dates."
            : "Analytics unavailable. Check local Visual Studio ADO sign-in and server configuration.",
            traceId = context.TraceIdentifier });
    }
});
app.UseCors("DashboardClient");
app.MapGet("/api/health", () => Results.Ok(new { status = "Healthy", adoConnected = (bool?)null,
    environment = app.Environment.EnvironmentName, reportingTimezone = "Europe/London" }));
app.MapGet("/api/auth/check", async (TokenCredential credential, CancellationToken ct) =>
{
    try
    {
        var token = await credential.GetTokenAsync(new TokenRequestContext(
            ["499b84ac-1321-427f-aa17-267ca6975798/.default"]), ct);
        return Results.Ok(new { authenticated = true, expiresOn = token.ExpiresOn,
            adoAuthorizationValidated = false });
    }
    catch (Exception error) when (error is CredentialUnavailableException or AuthenticationFailedException)
    {
        return Results.Ok(new { authenticated = false, reason = "Local organizational credential unavailable or failed." });
    }
});
app.MapGet("/api/ado/testplans", (FrontierAnalyticsService service, CancellationToken ct) => service.GetPlansAsync(ct));
app.MapGet("/api/ado/frontier/analytics/{planId:int}",
    (int planId, int? suiteId, string? tester, FrontierAnalyticsService service, CancellationToken ct) => service.GetAnalyticsAsync(planId, ct, suiteId, tester));
app.MapGet("/api/ado/frontier/suites/{planId:int}", async (int planId, FrontierAnalyticsService service, CancellationToken ct) =>
    new { value = await service.GetSuitesAsync(planId, ct) });
app.MapGet("/api/ado/frontier/points/{planId:int}/{suiteId:int}",
    async (int planId, int suiteId, FrontierAnalyticsService service, CancellationToken ct) =>
        new { value = await service.GetPointsAsync(planId, suiteId, ct) });
app.MapGet("/api/ado/frontier/history/{planId:int}", (int planId, int? suiteId, string from, string to,
    FrontierAnalyticsService service, CancellationToken ct) =>
{
    static DateTimeOffset Parse(string value)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(value, @"(Z|[+-]\d{2}:\d{2})$", System.Text.RegularExpressions.RegexOptions.IgnoreCase) ||
            !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            throw new ArgumentException("Dates require an explicit UTC offset.");
        return date;
    }
    return service.GetReexecutionAsync(planId, suiteId, new(Parse(from), Parse(to)), ct);
});
app.MapGet("/api/reporting/dsr-period", (Microsoft.Extensions.Options.IOptions<AzureDevOpsOptions> settings) =>
    ReportingClock.LastDsrPeriod(DateTimeOffset.UtcNow, TimeOnly.ParseExact(settings.Value.DsrCutoff, "HH:mm", CultureInfo.InvariantCulture)));
app.MapGet("/api/reporting/period", (string preset, Microsoft.Extensions.Options.IOptions<AzureDevOpsOptions> settings) =>
    ReportingClock.Preset(DateTimeOffset.UtcNow, preset, TimeOnly.ParseExact(settings.Value.DsrCutoff, "HH:mm", CultureInfo.InvariantCulture)));
app.MapGet("/api/v2/activity/{planId:int}", (int planId, int? suiteId, string from, string to, string baselineFrom,
    string? tester, ExecutionHistoryService service, CancellationToken ct) =>
    service.ActivityAsync(planId, suiteId, ReportingScope.Window(from, to), ReportingScope.Instant(baselineFrom), tester, ct));
app.MapControllers();
app.MapPost("/api/v2/reports", (Sandpiper.QA.Dashboard.Server.Models.ReportRequest request, ReportService service, CancellationToken ct) => service.CreateAsync(request, ct));
app.MapGet("/api/v2/reports", (ReportStore store) => store.List());
app.MapPost("/api/v2/reports/import", (Sandpiper.QA.Dashboard.Server.Models.ReportSnapshot report, ReportService service) => service.Import(report));
app.MapGet("/api/v2/reports/{id:guid}", (Guid id, ReportStore store) => store.Get(id) is { } report ? Results.Ok(report) : Results.NotFound());
app.MapGet("/api/v2/reports/{id:guid}/{format}", (Guid id, string format, ReportStore store) =>
{
    if (store.Get(id) is not { } report) return Results.NotFound();
    return format switch
    {
        "xlsx" => Results.File(ReportExporter.Excel(ReportService.Tables(report)), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Sandpiper-{report.Kind}-{id}.xlsx"),
        "pdf" => Results.File(ReportExporter.Pdf(report), "application/pdf", $"Sandpiper-{report.Kind}-{id}.pdf"),
        "json" => Results.File(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(report, ReportStore.Json), "application/json", $"Sandpiper-evidence-{id}.json"),
        _ => Results.BadRequest()
    };
});
app.Run();

public partial class Program;
