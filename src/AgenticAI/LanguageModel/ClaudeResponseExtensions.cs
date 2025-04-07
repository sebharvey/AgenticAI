using AgenticAI.Assistant.Models;

namespace AgenticAI.Assistant.LanguageModel
{
    /// <summary>
    /// Extension method to get content as string
    /// </summary>
    public static class ClaudeResponseExtensions
    {
        public static string GetTextContent(this ClaudeResponse response)
        {
            if (response?.Content == null || response.Content.Count == 0)
            {
                return string.Empty;
            }

            return string.Join("\n", response.Content
                .Where(c => c.Type == "text")
                .Select(c => c.Text));
        }
    }
}