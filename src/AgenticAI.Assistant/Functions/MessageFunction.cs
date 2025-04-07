using System.Net;
using System.Text.Json;
using AgenticAI.Assistant.Models;
using AgenticAI.Assistant.Orchestrator;
using AgenticAI.Assistant.Tooling;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace AgenticAI.Assistant.Functions
{
    /// <summary>
    /// Implementation of the Claude Agent message handling functions
    /// This class can be used directly in Azure Function projects
    /// </summary>
    public class MessageFunction
    {
        private readonly IAgentOrchestrator _orchestrator;
        private readonly IEnumerable<ITool> _tools;
        private readonly ILogger<MessageFunction> _logger;

        // Dictionary to store conversation history for each session
        // Note: In production, use a persistent storage like Azure Table Storage
        private static readonly Dictionary<string, List<Message>> SessionConversations = new();

        public MessageFunction(
            IAgentOrchestrator orchestrator,
            ILoggerFactory loggerFactory,
            IEnumerable<ITool> tools)
        {
            _orchestrator = orchestrator;
            _logger = loggerFactory.CreateLogger<MessageFunction>();
            _tools = tools;

            _logger.LogInformation("MessageFunction initialized");
        }

        /// <summary>
        /// Processes a message sent to the Claude Agent
        /// </summary>
        [Function("ProcessMessage")]
        public async Task<HttpResponseData> ProcessMessage(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "messages")] HttpRequestData req)
        {
            _logger.LogInformation("Processing message request");

            try
            {
                // Read the request body
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                _logger.LogInformation($"Request body: {requestBody}");

                var request = JsonSerializer.Deserialize<MessageRequest>(requestBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                // Validate request
                if (request == null || string.IsNullOrWhiteSpace(request.Message))
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteAsJsonAsync(new { error = "Message cannot be empty" });
                    return badResponse;
                }

                // Get or create conversation history for this session
                var sessionId = request.SessionId;
                if (!SessionConversations.ContainsKey(sessionId))
                {
                    SessionConversations[sessionId] = new List<Message>();
                    _logger.LogInformation($"Created new session: {sessionId}");
                }

                _logger.LogInformation($"Processing message for session {sessionId}: {request.Message}");

                foreach (ITool tool in _tools)
                {
                    _orchestrator.RegisterTool(tool);
                }

                // Process the message with conversation history
                var response = await _orchestrator.ProcessUserMessageAsync(
                    request.Message,
                    SessionConversations[sessionId]);

                _logger.LogInformation($"Response generated: {response.Substring(0, Math.Min(100, response.Length))}...");

                // Create the response
                var functionResponse = req.CreateResponse(HttpStatusCode.OK);
                await functionResponse.WriteAsJsonAsync(new { response });

                return functionResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message");

                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(new
                {
                    error = $"An error occurred processing your request: {ex.Message}",
                    stackTrace = ex.StackTrace
                });

                return errorResponse;
            }
        }

        /// <summary>
        /// Clears a conversation session
        /// </summary>
        [Function("ClearSession")]
        public async Task<HttpResponseData> ClearSession(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "sessions/{sessionId}")] HttpRequestData req,
            string sessionId)
        {
            _logger.LogInformation($"Clearing session: {sessionId}");

            if (SessionConversations.ContainsKey(sessionId))
            {
                SessionConversations.Remove(sessionId);

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { message = "Session cleared successfully" });

                return response;
            }

            var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
            await notFoundResponse.WriteAsJsonAsync(new { error = "Session not found" });

            return notFoundResponse;
        }

        /// <summary>
        /// Gets conversation history for a session
        /// </summary>
        [Function("GetSessionHistory")]
        public async Task<HttpResponseData> GetSessionHistory(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "sessions/{sessionId}")] HttpRequestData req,
            string sessionId)
        {
            _logger.LogInformation($"Getting history for session: {sessionId}");

            if (SessionConversations.ContainsKey(sessionId))
            {
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new { history = SessionConversations[sessionId] });

                return response;
            }

            var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
            await notFoundResponse.WriteAsJsonAsync(new { error = "Session not found" });

            return notFoundResponse;
        }

        /// <summary>
        /// Returns a simple heartbeat response to verify the function app is running
        /// </summary>
        [Function("Heartbeat")]
        public async Task<HttpResponseData> Heartbeat(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "heartbeat")] HttpRequestData req)
        {
            _logger.LogInformation("Heartbeat function triggered");

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                status = "alive",
                timestamp = DateTime.UtcNow.ToString("o"),
                message = "Claude Agent API is operational"
            });

            return response;
        }

        public class MessageRequest
        {
            public string Message { get; set; }
            public string SessionId { get; set; }
        }
    }
}