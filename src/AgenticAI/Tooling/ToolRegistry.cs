using AgenticAI.Assistant.Models;
using Microsoft.Extensions.Logging;

namespace AgenticAI.Assistant.Tooling
{
    /// <summary>
    /// Implementation of tool registry
    /// </summary>
    public class ToolRegistry : IToolRegistry
    {
        private readonly Dictionary<string, ITool> _tools = new Dictionary<string, ITool>();
        private readonly ILogger<ToolRegistry> _logger;

        public ToolRegistry(ILogger<ToolRegistry> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Registers a tool in the registry
        /// </summary>
        public void RegisterTool(ITool tool)
        {
            _tools[tool.Definition.Name] = tool;
            _logger.LogInformation($"Tool {tool.Definition.Name} registered");
        }

        /// <summary>
        /// Gets all available tools for Claude
        /// </summary>
        public List<Tool> GetAvailableTools()
        {
            return _tools.Values.Select(t => t.Definition).ToList();
        }

        /// <summary>
        /// Gets a tool by name
        /// </summary>
        public ITool GetToolByName(string name)
        {
            if (_tools.TryGetValue(name, out var tool))
            {
                return tool;
            }

            throw new KeyNotFoundException($"Tool with name {name} not found");
        }

        /// <summary>
        /// Checks if a tool exists
        /// </summary>
        public bool HasTool(string name)
        {
            return _tools.ContainsKey(name);
        }
    }
}