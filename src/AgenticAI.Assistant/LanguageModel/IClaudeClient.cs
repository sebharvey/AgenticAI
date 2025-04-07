using AgenticAI.Assistant.Models;

namespace AgenticAI.Assistant.LanguageModel
{
    /// <summary>
    /// Interface for the Claude API client
    /// </summary>
    public interface IClaudeClient
    {
        Task<ClaudeResponse> SendMessageAsync(string userMessage, List<Message> conversationHistory = null, List<Tool> availableTools = null);
    }
}