namespace TestAiAgent.Models
{
    /// <summary>
    /// Configuration options for Claude API
    /// </summary>
    public class ClaudeOptions
    {
        public string ApiKey { get; set; }
        public string ModelName { get; set; } = "claude-3-7-sonnet-20250219";
        public string ApiEndpoint { get; set; } = "https://api.anthropic.com/v1/messages";
        public double Temperature { get; set; } = 0.7;
        public int MaxTokens { get; set; } = 4096;
    }
}