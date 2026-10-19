namespace Sandpiper.QA.Dashboard.Server.Services
{
    public class AzureDevOpsService
    {
        private readonly AzureDevOpsOptions _options;

        public AzureDevOpsService(HttpClient httpClient, Microsoft.Extensions.Options.IOptions<AzureDevOpsOptions> options)
        {
            _options = options.Value;
        }
        private const int TestPlanId = 819259;

        public string GetTestSuitesApiUrl()
        {
            return _options.BaseUrl +
                   $"/_apis/testplan/Plans/{TestPlanId}/suites" +
                   "?asTreeView=true&api-version=7.1";
        }

        public string GetTestPointsApiUrl(int suiteId)
        {
            return _options.BaseUrl +
                   $"/_apis/testplan/Plans/{TestPlanId}" +
                   $"/Suites/{suiteId}/TestPoint?api-version=7.1";
        }
    }
}
