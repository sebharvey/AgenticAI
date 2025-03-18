using System.Text.Json.Serialization;

namespace TestAiAgent.Models
{
    /// <summary>
    /// Tool model for describing available tools to Claude
    /// </summary>
    public class Tool
    {
        public string Name { get; set; }
        public string Description { get; set; }

        [JsonPropertyName("input_schema")]
        public InputSchema InputSchema { get; set; }
    }
}