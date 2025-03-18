using System.Text.Json;

namespace TestAiAgent.Models
{
    /// <summary>
    /// Content block model for Claude messages
    /// </summary>
    public class ContentBlock
    {
        public string Type { get; set; }
        public string Text { get; set; }
        public string Id { get; set; }
        public string Name { get; set; }
        public JsonElement Input { get; set; }
    }
}