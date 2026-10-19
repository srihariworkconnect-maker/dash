
using Microsoft.AspNetCore.Mvc;
using Sandpiper.QA.Dashboard.Server.Services;

namespace Sandpiper.QA.Dashboard.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestAnalyticsController : ControllerBase
    {
        private readonly AzureDevOpsService _adoService;

        public TestAnalyticsController(AzureDevOpsService
            adoService)
        {
            _adoService = adoService;
        }

        [HttpGet("health")]
        public IActionResult GetHealth()
        {
            return Ok(new
            {
                status = "Healthy",
                application = "Sandpiper QA Command Center",
                adoConnected = (bool?)null,
                timestamp = DateTimeOffset.UtcNow
            });
        }

        [HttpGet("configuration")]
        public IActionResult GetConfiguration()
        {
            return Ok(new
            {
                testPlan = "Frontier AI",
                planId = 819259,
                suitesApiUrl = _adoService.GetTestSuitesApiUrl(),
                sampleTestPointsApiUrl =
                    _adoService.GetTestPointsApiUrl(819374),
                authenticationConfigured = "Local VisualStudioCredential; live authorization not established by this endpoint",
                additionalPlansSupported = true
            });
        }
    }
}
