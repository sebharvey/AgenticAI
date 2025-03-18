using System.Text.Json.Serialization;

namespace TestAiAgent.Models
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