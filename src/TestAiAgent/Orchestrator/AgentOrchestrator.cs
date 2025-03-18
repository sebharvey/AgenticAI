using Microsoft.Extensions.Logging;
using TestAiAgent.LanguageModel;
using TestAiAgent.Models;
using TestAiAgent.Tooling;
using TestAiAgent.Tooling.Tools;

namespace TestAiAgent.Orchestrator
{
    /// <summary>
    /// Updated implementation of the agent orchestrator
    /// </summary>
    public class AgentOrchestrator : IAgentOrchestrator
    {
        private readonly IClaudeClient _claudeClient;
        private readonly IToolRegistry _toolRegistry;
        private readonly IWeatherTool _weatherTool;
        private readonly ILogger<AgentOrchestrator> _logger;
        private static bool _toolsRegistered;
        private static readonly object InitLock = new();

        public AgentOrchestrator(
            IClaudeClient claudeClient,
            IToolRegistry toolRegistry,
            IWeatherTool weatherTool,
            ILogger<AgentOrchestrator> logger)
        {
            _claudeClient = claudeClient;
            _toolRegistry = toolRegistry;
            _weatherTool = weatherTool;
            _logger = logger;

            // Ensure tools are registered
            EnsureToolsRegistered();
        }

        /// <summary>
        /// Ensures tools are registered with the registry
        /// </summary>
        private void EnsureToolsRegistered()
        {
            if (!_toolsRegistered)
            {
                lock (InitLock)
                {
                    if (!_toolsRegistered)
                    {
                        _toolRegistry.RegisterTool(_weatherTool);
                        _logger.LogInformation("Weather tool registered successfully");
                        _toolsRegistered = true;
                    }
                }
            }
        }

        /// <summary>
        /// Processes a user message by sending it to Claude and handling tool calls
        /// </summary>
        public async Task<string> ProcessUserMessageAsync(string userMessage, List<Message> conversationHistory = null)
        {
            try
            {
                // Initialize conversation history if not provided
                conversationHistory ??= new List<Message>();

                EnsureToolsRegistered();

                var availableTools = _toolRegistry.GetAvailableTools();

                var claudeResponse = await _claudeClient.SendMessageAsync(userMessage, conversationHistory, availableTools);

                if (claudeResponse.StopReason == "tool_use")
                {
                    var toolUse = claudeResponse.Content.SingleOrDefault(item => item.Type == "tool_use");
                    var toolName = toolUse?.Name;
                    var toolId = toolUse?.Id;

                    if (_toolRegistry.HasTool(toolName))
                    {
                        // Get the tool and execute it
                        var tool = _toolRegistry.GetToolByName(toolName);
                        var toolResult = await tool.ExecuteAsync(toolUse.Input);

                        // Remove the last message that was automatically added by SendMessageAsync
                        // This ensures we don't duplicate the assistant message
                        conversationHistory.RemoveAt(conversationHistory.Count - 1);

                        // Manually add Claude's response that requested the tool with all properties preserved
                        conversationHistory.Add(new Message
                        {
                            Role = "assistant",
                            Content = claudeResponse.Content.Select(c => new ContentItem
                            {
                                Type = c.Type,
                                Text = c.Text,
                                Id = c.Id,
                                Name = c.Name,
                                Input = c.Type == "tool_use" ? c.Input : null,
                                // Do NOT set ToolUseId for tool_use - it's only for tool_result
                                ToolUseId = null
                            }).ToList()
                        });

                        // Create a proper tool result message that matches Claude's expected format
                        var toolResultMessage = new Message
                        {
                            Role = "user",
                            Content = new List<ContentItem>
                            {
                                new ContentItem
                                {
                                    Type = "tool_result",
                                    Content = toolResult,  // Use Content not Text for tool_result
                                    ToolUseId = toolId  // This is the critical field Claude expects
                                }
                            }
                        };

                        // Add the tool result to the conversation history
                        conversationHistory.Add(toolResultMessage);

                        // Send an empty message to Claude to get its final response
                        // The empty string is fine since we've added the tool_result to the conversation history
                        _logger.LogInformation("Sending tool result back to Claude for final response");
                        var finalResponse = await _claudeClient.SendMessageAsync(
                            "", // Empty string as we've already added the tool result to history
                            conversationHistory,
                            availableTools); // Make sure to include tools in case Claude needs them again

                        return finalResponse.GetTextContent();
                    }

                    _logger.LogWarning($"Claude requested a tool that doesn't exist: {toolName}");
                    return $"I apologize, but I don't have access to the tool '{toolName}' that would help answer your question.";
                }

                // If no tool was used, return Claude's response directly
                _logger.LogInformation("No tool was used, returning Claude's direct response");
                return claudeResponse.GetTextContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing user message");
                return $"I'm sorry, I encountered an error processing your request: {ex.Message}";
            }
        }
    }
}