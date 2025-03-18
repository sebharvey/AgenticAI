using System.Text.Json;
using TestAiAgent.Models;

namespace TestAiAgent.Tooling
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