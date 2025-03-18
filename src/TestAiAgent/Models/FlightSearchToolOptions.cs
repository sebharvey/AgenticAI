namespace TestAiAgent.Models;

/// <summary>
/// Configuration options for the flight search tool
/// </summary>
public class FlightSearchToolOptions
{
    public string ApiEndpoint { get; set; } = "https://testresources.virginatlantic.com/sit/graphQL/Search/public";
}