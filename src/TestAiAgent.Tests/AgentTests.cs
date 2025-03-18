using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text;
using System.Text.Json;
using TestAiAgent.LanguageModel;
using TestAiAgent.Models;
using TestAiAgent.Orchestrator;
using TestAiAgent.Tooling;
using TestAiAgent.Tooling.Tools;
using Xunit;

namespace TestAiAgent.Tests
{
    public class AgentTests
    {
        [Fact]
        public async Task AgentOrchestrator_ProcessUserMessage_ReturnsClaudeResponse()
        {
            // Arrange
            var claudeClientMock = new Mock<IClaudeClient>();
            var toolRegistryMock = new Mock<IToolRegistry>();
            var weatherToolMock = new Mock<IWeatherTool>();
            var flightSearchToolMock = new Mock<IFlightSearchTool>();
            var loggerMock = new Mock<ILogger<AgentOrchestrator>>();

            var claudeResponse = new ClaudeResponse
            {
                StopReason = "end_turn",
                Content = new List<ContentBlock>
                {
                    new ContentBlock { Type = "text", Text = "This is a test response from Claude" }
                }
            };

            claudeClientMock
                .Setup(c => c.SendMessageAsync(It.IsAny<string>(), It.IsAny<List<Message>>(), It.IsAny<List<Tool>>()))
                .ReturnsAsync(claudeResponse);

            toolRegistryMock
                .Setup(t => t.GetAvailableTools())
                .Returns(new List<Tool>());

            var orchestrator = new AgentOrchestrator(
                claudeClientMock.Object,
                flightSearchToolMock.Object,
                toolRegistryMock.Object,
                weatherToolMock.Object,
                loggerMock.Object);

            // Act
            var result = await orchestrator.ProcessUserMessageAsync("Hello, Claude!", new List<Message>());

            // Assert
            Assert.Equal("This is a test response from Claude", result);
            claudeClientMock.Verify(c => c.SendMessageAsync("Hello, Claude!", It.IsAny<List<Message>>(), It.IsAny<List<Tool>>()), Times.Once);
        }

        [Fact]
        public async Task ClaudeClient_SendMessageAsync_ReturnsResponse()
        {
            // Arrange
            var httpClientFactoryMock = new Mock<IHttpClientFactory>();
            var optionsMock = new Mock<IOptions<ClaudeOptions>>();
            var loggerMock = new Mock<ILogger<ClaudeClient>>();

            // Setup mock response
            var mockResponse = @"{
                ""id"": ""msg_123"",
                ""type"": ""message"",
                ""role"": ""assistant"",
                ""content"": [
                    {
                        ""type"": ""text"",
                        ""text"": ""This is a test response from Claude API""
                    }
                ],
                ""model"": ""claude-3-7-sonnet-20250219"",
                ""stop_reason"": ""end_turn"",
                ""usage"": {
                    ""input_tokens"": 10,
                    ""output_tokens"": 20
                }
            }";

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(mockResponse, Encoding.UTF8, "application/json")
                });

            var httpClient = new HttpClient(handlerMock.Object);
            httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

            optionsMock.Setup(o => o.Value).Returns(new ClaudeOptions
            {
                ApiKey = "fake-api-key",
                ModelName = "claude-3-7-sonnet-20250219",
                ApiEndpoint = "https://api.anthropic.com/v1/messages",
                Temperature = 0.7,
                MaxTokens = 4096
            });

            var claudeClient = new ClaudeClient(
                httpClientFactoryMock.Object,
                optionsMock.Object,
                loggerMock.Object);

            // Act
            var result = await claudeClient.SendMessageAsync("Hello, Claude!", new List<Message>());

            // Assert
            Assert.NotNull(result);
            Assert.Equal("end_turn", result.StopReason);
            Assert.Equal("This is a test response from Claude API", result.Content[0].Text);

            // Verify HTTP request was made
            handlerMock.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri.ToString() == "https://api.anthropic.com/v1/messages"),
                ItExpr.IsAny<CancellationToken>()
            );
        }

        [Fact]
        public async Task WeatherTool_ExecuteAsync_ReturnsWeatherData()
        {
            // Arrange
            var httpClientFactoryMock = new Mock<IHttpClientFactory>();
            var optionsMock = new Mock<IOptions<ToolOptions>>();
            var loggerMock = new Mock<ILogger<WeatherTool>>();

            // Setup mock response
            var weatherResponse = @"{
                ""main"": {
                    ""temp"": 18.5,
                    ""feels_like"": 17.9,
                    ""humidity"": 65
                },
                ""weather"": [
                    {
                        ""description"": ""scattered clouds""
                    }
                ],
                ""wind"": {
                    ""speed"": 5.2
                },
                ""name"": ""Seattle""
            }";

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(weatherResponse, Encoding.UTF8, "application/json")
                });

            var httpClient = new HttpClient(handlerMock.Object);
            httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(httpClient);

            optionsMock
                .Setup(o => o.Value)
                .Returns(new ToolOptions
                {
                    Weather = new WeatherToolOptions
                    {
                        ApiKey = "fake-api-key",
                        ApiEndpoint = "https://api.openweathermap.org/data/2.5/weather"
                    }
                });

            var weatherTool = new WeatherTool(
                httpClientFactoryMock.Object,
                optionsMock.Object,
                loggerMock.Object);

            // Create input for tool
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var inputJson = JsonSerializer.Serialize(new { location = "Seattle", units = "metric" }, options);
            var inputElement = JsonDocument.Parse(inputJson).RootElement;

            // Act
            var result = await weatherTool.ExecuteAsync(inputElement);

            // Assert
            Assert.NotNull(result);
            var resultObj = JsonSerializer.Deserialize<JsonElement>(result);
            Assert.Equal("Seattle", resultObj.GetProperty("city").GetString());
            Assert.Contains("18.5", resultObj.GetProperty("temperature").GetString());
            Assert.Contains("scattered clouds", resultObj.GetProperty("description").GetString());

            // Verify HTTP request was made
            handlerMock.Protected().Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri.ToString().Contains("api.openweathermap.org/data/2.5/weather")),
                ItExpr.IsAny<CancellationToken>()
            );
        }

        [Fact]
        public void ToolRegistry_RegisterAndGetTools_WorksCorrectly()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<ToolRegistry>>();
            var toolRegistry = new ToolRegistry(loggerMock.Object);

            var weatherToolMock = new Mock<IWeatherTool>();
            weatherToolMock.Setup(t => t.Definition).Returns(new Tool
            {
                Name = "get_weather",
                Description = "Get weather information"
            });

            // Act
            toolRegistry.RegisterTool(weatherToolMock.Object);
            var availableTools = toolRegistry.GetAvailableTools();
            var hasTool = toolRegistry.HasTool("get_weather");
            var retrievedTool = toolRegistry.GetToolByName("get_weather");

            // Assert
            Assert.Single(availableTools);
            Assert.Equal("get_weather", availableTools[0].Name);
            Assert.True(hasTool);
            Assert.Equal(weatherToolMock.Object, retrievedTool);
        }
    }
}