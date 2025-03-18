using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TestAiAgent.Models;

namespace TestAiAgent.Tooling.Tools
{
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
            IOptions<ToolOptions> options,
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
                var location = input.GetProperty("location").GetString();
                var units = input.TryGetProperty("units", out var unitsElement) ? unitsElement.GetString() : "metric";

                // todo: probably don't need this anymore...
                var cityName = ParseCityName(location);

                var url = $"{_options.ApiEndpoint}?q={Uri.EscapeDataString(cityName)}&units={units}&appid={_options.ApiKey}";

                // Send the request
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var weatherData = await response.Content.ReadAsStringAsync();
                var weatherJson = JsonDocument.Parse(weatherData);

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
                    description,
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