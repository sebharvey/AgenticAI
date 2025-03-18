using TestAiAgent.Models;

namespace TestAiAgent.Tooling
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