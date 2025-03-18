using System.Text.Json.Serialization;

namespace TestAiAgent.Models
{
    /// <summary>
    /// Updated Claude API response model
    /// </summary>
    public class ClaudeResponse
    {
        public string Id { get; set; }
        public string Type { get; set; }

        [JsonPropertyName("content")]
        public List<ContentBlock> Content { get; set; }

        public string Role => "assistant";

        public string Model { get; set; }


        [JsonPropertyName("stop_reason")]
        public string StopReason { get; set; }

        [JsonPropertyName("stop_sequence")]
        public string StopSequence { get; set; }


        public Usage Usage { get; set; }
    }
}