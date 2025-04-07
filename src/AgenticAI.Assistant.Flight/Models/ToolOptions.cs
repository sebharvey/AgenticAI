namespace AgenticAI.Assistant.Flight.Models
{
    /// <summary>
    /// Configuration options for tools
    /// </summary>
    public class ToolOptions
    {
        public WeatherToolOptions Weather { get; set; }
        public FlightSearchToolOptions FlightSearch { get; set; }
    }
}