namespace TestAiAgent.Models
{
    /// <summary>
    /// Input schema for tool
    /// </summary>
    public class InputSchema
    {
        public string Type { get; set; } = "object";
        public Dictionary<string, SchemaProperty> Properties { get; set; }
        public List<string> Required { get; set; }
    }
}