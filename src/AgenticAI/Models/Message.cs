using System.Text.Json.Serialization;

namespace AgenticAI.Assistant.Models
{
    /// <summary>
    /// Updated Message model for the conversation
    /// </summary>
    public class Message
    {
        public string Role { get; set; }

        [JsonPropertyName("content")]
        public List<ContentItem> Content { get; set; } = new List<ContentItem>();
    }
}