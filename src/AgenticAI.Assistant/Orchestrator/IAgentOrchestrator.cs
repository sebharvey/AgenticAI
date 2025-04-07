
using AgenticAI.Assistant.Models;
using AgenticAI.Assistant.Tooling;

namespace AgenticAI.Assistant.Orchestrator
{
    /// <summary>
    /// Interface for the agent orchestrator that coordinates the Claude client and tools
    /// </summary>
    public interface IAgentOrchestrator
    {
        Task<string> ProcessUserMessageAsync(string userMessage, List<Message> conversationHistory = null);
        void RegisterTool(ITool tool);
    }
}