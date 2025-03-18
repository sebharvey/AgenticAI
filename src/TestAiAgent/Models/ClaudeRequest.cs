using System.Text.Json.Serialization;

namespace TestAiAgent.Models
{
    /// <summary>
    /// Updated Claude API request model
    /// </summary>
    public class ClaudeRequest
    {
        public string Model { get; set; }
        public List<Message> Messages { get; set; }

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; }

        public double Temperature { get; set; }
        public List<Tool> Tools { get; set; }
        public string System { get; set; }
    }
}