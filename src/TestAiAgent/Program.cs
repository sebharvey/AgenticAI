using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TestAiAgent.LanguageModel;
using TestAiAgent.Models;
using TestAiAgent.Orchestrator;
using TestAiAgent.Tooling;
using TestAiAgent.Tooling.Tools;

namespace TestAiAgent
{
    /// <summary>
    /// Main Program class that sets up and runs the Claude AI Agent
    /// </summary>
    public class Program
    {
        public static void Main(string[] args)
        {
            var host = new HostBuilder()
                .ConfigureFunctionsWebApplication()
                .ConfigureAppConfiguration((context, config) =>
                {
                    // Load configuration from appsettings.json
                    config.AddJsonFile("appsettings.json", optional: false);
                    config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true);

                    // Load configuration from Azure Key Vault
                    if (context.HostingEnvironment.IsProduction())
                    {
                        var builtConfig = config.Build();
                        var keyVaultUrl = builtConfig["KeyVault:Url"];

                        //config.AddAzureKeyVault(
                        //    new Uri(keyVaultUrl),
                        //    new DefaultAzureCredential());
                    }

                    // Add environment variables and command line arguments
                    config.AddEnvironmentVariables();
                    config.AddCommandLine(args);
                })
                .ConfigureServices((context, services) =>
                {
                    // Register services
                    services.Configure<ClaudeOptions>(context.Configuration.GetSection("Claude"));
                    services.Configure<ToolOptions>(context.Configuration.GetSection("Tools"));

                    // Register HTTP client
                    services.AddHttpClient();

                    // Register our services
                    services.AddSingleton<IClaudeClient, ClaudeClient>();
                    services.AddSingleton<IToolRegistry, ToolRegistry>();
                    services.AddSingleton<IWeatherTool, WeatherTool>();
                    services.AddSingleton<IAgentOrchestrator, AgentOrchestrator>();

                    // Configure JSON serialization globally
                    services.Configure<JsonSerializerOptions>(options =>
                    {
                        options.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
                        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                    });
                })
                .Build();

            host.Run();
        }
    }
}

namespace TestAiAgent.Models
{
    /// <summary>
    /// Configuration options for Claude API
    /// </summary>
    public class ClaudeOptions
    {
        public string ApiKey { get; set; }
        public string ModelName { get; set; } = "claude-3-7-sonnet-20250219";
        public string ApiEndpoint { get; set; } = "https://api.anthropic.com/v1/messages";
        public double Temperature { get; set; } = 0.7;
        public int MaxTokens { get; set; } = 4096;
    }

    /// <summary>
    /// Configuration options for tools
    /// </summary>
    public class ToolOptions
    {
        public WeatherToolOptions Weather { get; set; }
    }

    /// <summary>
    /// Configuration options for the weather tool
    /// </summary>
    public class WeatherToolOptions
    {
        public string ApiKey { get; set; }
        public string ApiEndpoint { get; set; } = "https://api.openweathermap.org/data/2.5/weather";
    }

    /// <summary>
    /// Updated Message model for the conversation
    /// </summary>
    public class Message
    {
        public string Role { get; set; }

        [JsonPropertyName("content")]
        public List<ContentItem> Content { get; set; } = new List<ContentItem>();
    }

    public class ContentItem
    {
        public string Type { get; set; } = "text";
        public string Text { get; set; }
        public string Id { get; set; }
        public string Name { get; set; }
        [JsonPropertyName("input")]
        public JsonElement? Input { get; set; }
        [JsonPropertyName("tool_use_id")]
        public string ToolUseId { get; set; }
    }

    /// <summary>
    /// Updated Claude API request model
    /// </summary>
    public class ClaudeRequest
    {
        public string Model { get; set; }
        public List<Message> Messages { get; set; }

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; }

        public double Temperature { get; set; }
        public List<Tool> Tools { get; set; }
        public string System { get; set; }
    }

    /// <summary>
    /// Updated Claude API response model
    /// </summary>
    public class ClaudeResponse
    {
        public string Id { get; set; }
        public string Type { get; set; }

        [JsonPropertyName("content")]
        public List<ContentBlock> Content { get; set; }

        public string Role => "assistant";

        public string Model { get; set; }


        [JsonPropertyName("stop_reason")]
        public string StopReason { get; set; }

        [JsonPropertyName("stop_sequence")]
        public string StopSequence { get; set; }


        public Usage Usage { get; set; }
    }

    /// <summary>
    /// Content block model for Claude messages
    /// </summary>
    public class ContentBlock
    {
        public string Type { get; set; }
        public string Text { get; set; }
        public string Id { get; set; }
        public string Name { get; set; }
        public JsonElement Input { get; set; }
    }


    /// <summary>
    /// Tool model for describing available tools to Claude
    /// </summary>
    public class Tool
    {
        public string Name { get; set; }
        public string Description { get; set; }

        [JsonPropertyName("input_schema")]
        public InputSchema InputSchema { get; set; }
    }

    /// <summary>
    /// Input schema for tool
    /// </summary>
    public class InputSchema
    {
        public string Type { get; set; } = "object";
        public Dictionary<string, SchemaProperty> Properties { get; set; }
        public List<string> Required { get; set; }
    }

    /// <summary>
    /// Schema property definition
    /// </summary>
    public class SchemaProperty
    {
        public string Type { get; set; }
        public string Description { get; set; }
    }

    /// <summary>
    /// Token usage information
    /// </summary>
    public class Usage
    {
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
    }

}

namespace TestAiAgent.LanguageModel
{
    /// <summary>
    /// Interface for the Claude API client
    /// </summary>
    public interface IClaudeClient
    {
        Task<ClaudeResponse> SendMessageAsync(string userMessage, List<Message> conversationHistory = null, List<Tool> availableTools = null);
    }

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

            // Configure HTTP client with default headers
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
                // Create a new list if conversation history is null
                conversationHistory ??= new List<Message>();

                // Add the current user message to the history with correct content format
                conversationHistory.Add(new Message
                {
                    Role = "user",
                    Content = new List<ContentItem>
                    {
                        new ContentItem { Type = "text", Text = userMessage }
                    }
                });

                _logger.LogInformation($"Sending message to Claude. Message length: {userMessage.Length} chars");
                _logger.LogInformation($"Conversation history: {conversationHistory.Count} messages");

                // Prepare the request payload
                var requestPayload = new ClaudeRequest
                {
                    Model = _options.ModelName,
                    Messages = conversationHistory,
                    MaxTokens = _options.MaxTokens,
                    Temperature = _options.Temperature
                };

                // Add tools if provided
                if (availableTools is { Count: > 0 })
                {
                    requestPayload.Tools = availableTools;
                }

                // Serialize the request
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };

                var jsonContent = JsonSerializer.Serialize(requestPayload, jsonOptions);
                _logger.LogInformation($"Request payload: {jsonContent}");

                // Send the request to the Claude API
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                _logger.LogInformation($"Sending request to {_options.ApiEndpoint}");
                var response = await _httpClient.PostAsync(_options.ApiEndpoint, content);

                // Log response status
                _logger.LogInformation($"Claude API response status: {response.StatusCode}");

                // Get response content even if it's an error
                var responseString = await response.Content.ReadAsStringAsync();
                _logger.LogInformation($"Response content: {responseString}");

                // Ensure the request was successful
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Claude API error: {response.StatusCode}, Content: {responseString}");
                    throw new HttpRequestException($"Claude API returned {response.StatusCode}: {responseString}");
                }

                // Parse the response
                var claudeResponse = JsonSerializer.Deserialize<ClaudeResponse>(responseString, jsonOptions);

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
                        Input = c.Type == "tool_use" ? c.Input : null
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
            var toolName = tool.Definition.Name;

            if (_tools.ContainsKey(toolName))
            {
                _logger.LogWarning($"Tool with name {toolName} already exists and will be replaced");
            }

            _tools[toolName] = tool;
            _logger.LogInformation($"Tool {toolName} registered");
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

    /// <summary>
    /// Interface for a tool that can be used by Claude
    /// </summary>
    public interface ITool
    {
        Tool Definition { get; }
        Task<string> ExecuteAsync(JsonElement input);
    }

}

namespace TestAiAgent.Tooling.Tools
{
    /// <summary>
    /// Weather tool implementation for retrieving weather data
    /// </summary>
    public interface IWeatherTool : ITool
    {
    }

    /// <summary>
    /// Weather tool implementation
    /// </summary>
    public class WeatherTool : IWeatherTool
    {
        private readonly HttpClient _httpClient;
        private readonly WeatherToolOptions _options;
        private readonly ILogger<WeatherTool> _logger;

        public WeatherTool(
            IHttpClientFactory httpClientFactory,
            Microsoft.Extensions.Options.IOptions<ToolOptions> options,
            ILogger<WeatherTool> logger)
        {
            _httpClient = httpClientFactory.CreateClient();
            _options = options.Value.Weather;
            _logger = logger;
        }

        /// <summary>
        /// Tool definition for Claude
        /// </summary>
        public Tool Definition => new Tool
        {
            Name = "get_weather",
            Description = "Get current weather information for a specific location. The API works best with just city names without state/country information.",
            InputSchema = new InputSchema
            {
                Type = "object",
                Properties = new Dictionary<string, SchemaProperty>
                {
                    {
                        "location", new SchemaProperty
                        {
                            Type = "string",
                            Description = "The city name (e.g., 'New York' or 'London'). Just the city name works best - no need to include state or country."
                        }
                    },
                    {
                        "units", new SchemaProperty
                        {
                            Type = "string",
                            Description = "Temperature units: 'metric' for Celsius, 'imperial' for Fahrenheit"
                        }
                    }
                },
                Required = new List<string> { "location" }
            }
        };

        /// <summary>
        /// Executes the weather tool to get weather data
        /// </summary>
        public async Task<string> ExecuteAsync(JsonElement input)
        {
            try
            {
                // Extract input parameters
                var location = input.GetProperty("location").GetString();
                var units = input.TryGetProperty("units", out var unitsElement)
                    ? unitsElement.GetString()
                    : "metric";

                // Parse the location to extract just the city name
                var cityName = ParseCityName(location);

                // Build the API URL
                var url = $"{_options.ApiEndpoint}?q={Uri.EscapeDataString(cityName)}&units={units}&appid={_options.ApiKey}";

                // Send the request
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                // Parse the response
                var weatherData = await response.Content.ReadAsStringAsync();
                var weatherJson = JsonDocument.Parse(weatherData);

                // Format a user-friendly response
                var root = weatherJson.RootElement;
                var main = root.GetProperty("main");
                var weather = root.GetProperty("weather")[0];
                var wind = root.GetProperty("wind");

                var temp = main.GetProperty("temp").GetDouble();
                var feelsLike = main.GetProperty("feels_like").GetDouble();
                var description = weather.GetProperty("description").GetString();
                var windSpeed = wind.GetProperty("speed").GetDouble();
                var apiCityName = root.GetProperty("name").GetString();

                var unitSymbol = units == "metric" ? "°C" : "°F";
                var windUnit = units == "metric" ? "m/s" : "mph";

                return JsonSerializer.Serialize(new
                {
                    city = apiCityName,
                    temperature = $"{temp}{unitSymbol}",
                    feels_like = $"{feelsLike}{unitSymbol}",
                    description = description,
                    wind_speed = $"{windSpeed} {windUnit}",
                    humidity = $"{main.GetProperty("humidity").GetInt32()}%",
                    raw_data = weatherJson.RootElement
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing weather tool");
                return JsonSerializer.Serialize(new
                {
                    error = $"Failed to retrieve weather data: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Parses a location string to extract just the city name
        /// </summary>
        /// todo: I think this can be removed, this should be formatted by the LLM based on the definition
        private string ParseCityName(string location)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                return string.Empty;
            }

            // If the location contains a comma, take only the part before the first comma
            // This handles formats like "New York, NY" or "London, UK"
            var parts = location.Split(',');
            return parts[0].Trim();
        }
    }
}

namespace TestAiAgent.Orchestrator
{
    /// <summary>
    /// Interface for the agent orchestrator that coordinates the Claude client and tools
    /// </summary>
    public interface IAgentOrchestrator
    {
        Task<string> ProcessUserMessageAsync(string userMessage, List<Message> conversationHistory = null);
    }

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

                // Ensure tools are registered
                EnsureToolsRegistered();

                // Get available tools
                var availableTools = _toolRegistry.GetAvailableTools();
                _logger.LogInformation($"Available tools for processing: {availableTools.Count}");

                // Send the message to Claude
                _logger.LogInformation("Sending message to Claude with tools");
                var claudeResponse = await _claudeClient.SendMessageAsync(userMessage, conversationHistory, availableTools);
                _logger.LogInformation("Received response from Claude");

                // Check if Claude wants to use a tool
                if (claudeResponse.StopReason == "tool_use")
                {
                    var toolUse = claudeResponse.Content.SingleOrDefault(item => item.Type == "tool_use");
                    var toolName = toolUse?.Name;
                    var toolId = toolUse?.Id;

                    _logger.LogInformation($"Claude requested to use tool: {toolName} with ID: {toolId}");

                    if (_toolRegistry.HasTool(toolName))
                    {
                        // Get the tool and execute it
                        var tool = _toolRegistry.GetToolByName(toolName);
                        _logger.LogInformation($"Executing tool: {toolName}");
                        var toolResult = await tool.ExecuteAsync(toolUse.Input);
                        _logger.LogInformation("Tool execution completed");

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
                                Input = c.Type == "tool_use" ? c.Input : null
                            }).ToList()
                        });

                        // Now add the tool result as a user message with the correct tool_use_id field
                        conversationHistory.Add(new Message
                        {
                            Role = "user",
                            Content = new List<ContentItem> {
                        new()
                        {
                            Type = "tool_result",
                            Text = toolResult,
                            // Note: We keep the Id field but also set the ToolUseId field for the API
                            Id = toolId,
                            ToolUseId = toolId,  // This is the critical field Claude expects
                            Name = toolName
                        }
                    }
                        });

                        // Send the tool result back to Claude WITHOUT adding an additional message
                        // The tool result is already in the conversation history as a structured message
                        _logger.LogInformation("Sending tool result back to Claude");
                        var finalResponse = await _claudeClient.SendMessageAsync(
                            "", // Empty string instead of tool result as text
                            conversationHistory);

                        return finalResponse.GetTextContent();
                    }
                    else
                    {
                        _logger.LogWarning($"Claude requested a tool that doesn't exist: {toolName}");
                        return $"I apologize, but I don't have access to the tool '{toolName}' that would help answer your question.";
                    }
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