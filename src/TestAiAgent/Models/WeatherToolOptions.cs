namespace TestAiAgent.Models
{
    /// <summary>
    /// Configuration options for the weather tool
    /// </summary>
    public class WeatherToolOptions
    {
        public string ApiKey { get; set; }
        public string ApiEndpoint { get; set; } = "https://api.openweathermap.org/data/2.5/weather";
    }
}