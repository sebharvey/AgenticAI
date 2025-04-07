using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgenticAI.Assistant.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgenticAI.Assistant.LanguageModel.Claude
{
    /// <summary>
    /// Updated implementation of the Claude API client
    /// </summary>
    public class ClaudeClient : IClaudeClient
    {
        private readonly HttpClient _httpClient;
        private readonly ClaudeOptions _options;
        private readonly ILogger<ClaudeClient> _logger;

        public ClaudeClient(
            IHttpClientFactory httpClientFactory,
            IOptions<ClaudeOptions> options,
            ILogger<ClaudeClient> logger)
        {
            _httpClient = httpClientFactory.CreateClient();
            _options = options.Value;
            _logger = logger;

            _httpClient.DefaultRequestHeaders.Add("x-api-key", _options.ApiKey);
            _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

            _logger.LogInformation($"ClaudeClient initialized with model: {_options.ModelName}");
        }

        /// <summary>
        /// Sends a message to Claude API and gets the response
        /// </summary>
        public async Task<ClaudeResponse> SendMessageAsync(string userMessage, List<Message> conversationHistory = null, List<Tool> availableTools = null)
        {
            try
            {
                conversationHistory ??= new List<Message>();

                // Only add a new user message if it's not empty
                // This allows us to send requests with just the conversation history
                // which is useful for tool results
                if (!string.IsNullOrWhiteSpace(userMessage))
                {
                    conversationHistory.Add(new Message
                    {
                        Role = "user",
                        Content = new List<ContentItem>
                        {
                            new ContentItem { Type = "text", Text = userMessage }
                        }
                    });

                    _logger.LogInformation($"Added user message to conversation. Message length: {userMessage.Length} chars");
                }

                _logger.LogInformation($"Sending request to Claude with {conversationHistory.Count} messages in history");

                var requestPayload = new ClaudeRequest
                {
                    Model = _options.ModelName,
                    Messages = conversationHistory,
                    MaxTokens = _options.MaxTokens,
                    Temperature = _options.Temperature
                };

                if (availableTools is { Count: > 0 })
                {
                    requestPayload.Tools = availableTools;
                }

                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };

                var jsonContent = JsonSerializer.Serialize(requestPayload, jsonOptions);
                _logger.LogInformation($"Request payload: {jsonContent}");

                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                _logger.LogInformation($"Sending request to {_options.ApiEndpoint}");
                var response = await _httpClient.PostAsync(_options.ApiEndpoint, content);

                _logger.LogInformation($"Claude API response status: {response.StatusCode}");

                // Get response content even if it's an error
                var responseString = await response.Content.ReadAsStringAsync();
                _logger.LogInformation($"Response content: {responseString}");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Claude API error: {response.StatusCode}, Content: {responseString}");
                    throw new HttpRequestException($"Claude API returned {response.StatusCode}: {responseString}");
                }

                var claudeResponse = JsonSerializer.Deserialize<ClaudeResponse>(responseString, jsonOptions);

                // Log the detailed response structure to help with debugging
                _logger.LogInformation($"Claude response deserialized with {claudeResponse.Content?.Count ?? 0} content items");
                foreach (var item in claudeResponse.Content ?? new List<ContentBlock>())
                {
                    _logger.LogInformation($"Content item: Type={item.Type}, Id={item.Id}, Name={item.Name}, Text={item.Text?.Substring(0, Math.Min(item.Text?.Length ?? 0, 30)) ?? "null"}...");
                }

                // Add assistant's response to conversation history with proper content format
                // Make sure to preserve ALL properties for tool interactions
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
                        // Only set ToolUseId for tool_result items, not for tool_use items
                        ToolUseId = c.Type == "tool_result" ? c.Id : null
                    }).ToList()
                });

                return claudeResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error communicating with Claude API");
                throw;
            }
        }
    }
}