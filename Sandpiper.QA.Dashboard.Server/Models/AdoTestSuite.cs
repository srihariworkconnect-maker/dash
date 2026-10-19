
using System.Text.Json.Serialization;

namespace Sandpiper.QA.Dashboard.Server.Models
{
    public class AdoTestSuite
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("suiteType")]
        public string? SuiteType { get; set; }

        [JsonPropertyName("children")]
        public List<AdoTestSuite> Children { get; set; } = new();
    }

    public class AdoTestSuiteResponse
    {
        [JsonPropertyName("value")]
        public List<AdoTestSuite> Value { get; set; } = new();

        [JsonPropertyName("count")]
        public int Count { get; set; }
    }
}
