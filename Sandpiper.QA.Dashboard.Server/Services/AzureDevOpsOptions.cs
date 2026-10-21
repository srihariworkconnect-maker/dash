namespace Sandpiper.QA.Dashboard.Server.Services;

public sealed class AzureDevOpsOptions
{
    public string Organization { get; set; } = "";
    public string Project { get; set; } = "";
    public string TenantId { get; set; } = "";
    public int MaxConcurrency { get; set; } = 4;
    public string DsrCutoff { get; set; } = "18:00";
    public int CacheSeconds { get; set; } = 30;
    // Upper bound for acquiring an Azure DevOps access token. VisualStudioCredential can block on a
    // broker/IDE handshake inside a VDI; without a bound a single stalled sign-in leaves the whole
    // evidence request pending indefinitely. A breach fails the request with an explicit error.
    public int TokenTimeoutSeconds { get; set; } = 30;

    public string BaseUrl => $"https://dev.azure.com/{Uri.EscapeDataString(Organization)}/{Uri.EscapeDataString(Project)}";
    // Organization-scoped base for the few WIT APIs that exist only at organization level
    // (e.g. wit/workitemrelationtypes); the project-scoped path returns 404 for these.
    public string OrganizationBaseUrl => $"https://dev.azure.com/{Uri.EscapeDataString(Organization)}";
}
