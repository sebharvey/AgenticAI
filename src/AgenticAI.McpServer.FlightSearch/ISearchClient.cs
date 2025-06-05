namespace AgenticAI.McpServer.FlightSearch;

public interface ISearchClient
{
    /// <summary>
    /// Executes the flight search tool to get flight availability data
    /// </summary>
    Task<string> ExecuteAsync(SearchRequest searchRequest);
}