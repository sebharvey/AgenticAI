using System.Text.Json;
using System.Text.Json.Serialization;

namespace TestAiAgent.Models
{
    public class ContentItem
    {
        public string Type { get; set; } = "text";

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("text")]
        public string Text { get; set; }

        // Only used for tool_result type - this should be used instead of Text for tool_result
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        [JsonPropertyName("content")]
        public string Content { get; set; }

        // Only used for tool_use type
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Id { get; set; }

        // Only used for tool_use type
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Name { get; set; }

        // Only used for tool_use type
        [JsonPropertyName("input")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public JsonElement? Input { get; set; }

        // Only used for tool_result type
        [JsonPropertyName("tool_use_id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string ToolUseId { get; set; }
    }
}