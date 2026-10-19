namespace Sandpiper.QA.Dashboard.Server.Services;

public sealed class AzureDevOpsOptions
{
    public string Organization { get; set; } = "";
    public string Project { get; set; } = "";
    public string TenantId { get; set; } = "";
    public int MaxConcurrency { get; set; } = 4;
    public string DsrCutoff { get; set; } = "18:00";

    public string BaseUrl => $"https://dev.azure.com/{Uri.EscapeDataString(Organization)}/{Uri.EscapeDataString(Project)}";
}
