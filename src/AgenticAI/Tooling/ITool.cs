using System.Text.Json;
using AgenticAI.Assistant.Models;

namespace AgenticAI.Assistant.Tooling
{
    /// <summary>
    /// Interface for a tool that can be used by Claude
    /// </summary>
    public interface ITool
    {
        Tool Definition { get; }
        Task<string> ExecuteAsync(JsonElement input);
    }
}