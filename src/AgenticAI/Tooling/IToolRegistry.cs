using AgenticAI.Assistant.Models;

namespace AgenticAI.Assistant.Tooling
{
    /// <summary>
    /// Interface for tool registry to manage available tools
    /// </summary>
    public interface IToolRegistry
    {
        void RegisterTool(ITool tool);
        List<Tool> GetAvailableTools();
        ITool GetToolByName(string name);
        bool HasTool(string name);
    }
}