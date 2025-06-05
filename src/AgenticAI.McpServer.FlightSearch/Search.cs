using System.Net.Http;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AgenticAI.McpServer.FlightSearch
{
    public class Search
    {
        private readonly ILogger<Search> _logger;

        public Search(ILogger<Search> logger)
        {
            _logger = logger;
        }

        [Function("Function1")]
        public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");
            return new OkObjectResult("Welcome to Azure Functions!");
        }
    }

    public class SearchRequest
    {
        public string Origin { get; set; }
        public string Destination { get; set; }
        public DateTime DepartureDate { get; set; }

    }

    #region Response Models

    // Main response model
    public class FlightSearchGraphQLResponse
    {
        public GraphQLData Data { get; set; }
    }

    public class GraphQLData
    {
        public SearchOffersData SearchOffers { get; set; }
    }

    public class SearchOffersData
    {
        public ResultData Result { get; set; }
    }

    public class ResultData
    {
        public SlicesInfo Slices { get; set; }
        public CriteriaData Criteria { get; set; }
        public SliceData Slice { get; set; }
    }

    public class SlicesInfo
    {
        public int Current { get; set; }
        public int Total { get; set; }
    }

    public class CriteriaData
    {
        public LocationInfo Origin { get; set; }
        public LocationInfo Destination { get; set; }
        public string Departing { get; set; }
    }

    public class SliceData
    {
        public List<FlightAndFare> FlightsAndFares { get; set; }
    }

    public class FlightAndFare
    {
        public FlightData Flight { get; set; }
        public List<FareData> Fares { get; set; }
    }

    public class FlightData
    {
        public List<SegmentData> Segments { get; set; }
        public string Duration { get; set; }
        public LocationInfo Origin { get; set; }
        public LocationInfo Destination { get; set; }
        public string Departure { get; set; }
        public string Arrival { get; set; }
    }

    public class SegmentData
    {
        public AirlineInfo Airline { get; set; }
        public string FlightNumber { get; set; }
        public string OperatingFlightNumber { get; set; }
        public AirlineInfo OperatingAirline { get; set; }
        public LocationInfo Origin { get; set; }
        public LocationInfo Destination { get; set; }
        public string Duration { get; set; }
        public string Departure { get; set; }
        public string Arrival { get; set; }
        public int StopCount { get; set; }
    }

    public class AirlineInfo
    {
        public string Code { get; set; }
        public string Name { get; set; }
    }

    public class LocationInfo
    {
        public string Code { get; set; }
        public string CityName { get; set; }
        public string CountryName { get; set; }
        public string AirportName { get; set; }
    }

    public class FareData
    {
        public int? AvailableSeatCount { get; set; }
        public string FareFamilyType { get; set; }
        public PriceData Price { get; set; }
        public List<FareSegmentData> FareSegments { get; set; }
    }

    public class PriceData
    {
        public decimal AmountIncludingTax { get; set; }
        public string Currency { get; set; }
    }

    public class FareSegmentData
    {
        public string CabinName { get; set; }
    }

    #endregion

    #region Response Output Models

    public class FlightSearchResults
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("flights")]
        public List<FlightResult> Flights { get; set; }

        [JsonPropertyName("searchCriteria")]
        public SearchCriteria SearchCriteria { get; set; }

        [JsonPropertyName("flightCount")]
        public int FlightCount { get; set; }
    }

    public class SearchCriteria
    {
        [JsonPropertyName("origin")]
        public string Origin { get; set; }

        [JsonPropertyName("originCity")]
        public string OriginCity { get; set; }

        [JsonPropertyName("destination")]
        public string Destination { get; set; }

        [JsonPropertyName("destinationCity")]
        public string DestinationCity { get; set; }

        [JsonPropertyName("departureDate")]
        public string DepartureDate { get; set; }

        [JsonPropertyName("returnDate")]
        public string ReturnDate { get; set; }

        [JsonPropertyName("nonStopOnly")]
        public bool NonStopOnly { get; set; }
    }

    public class FlightResult
    {
        [JsonPropertyName("origin")]
        public string Origin { get; set; }

        [JsonPropertyName("originCity")]
        public string OriginCity { get; set; }

        [JsonPropertyName("destination")]
        public string Destination { get; set; }

        [JsonPropertyName("destinationCity")]
        public string DestinationCity { get; set; }

        [JsonPropertyName("departure")]
        public string Departure { get; set; }

        [JsonPropertyName("arrival")]
        public string Arrival { get; set; }

        [JsonPropertyName("duration")]
        public string Duration { get; set; }

        [JsonPropertyName("isNonStop")]
        public bool IsNonStop { get; set; }

        [JsonPropertyName("segments")]
        public List<SegmentInfo> Segments { get; set; }

        [JsonPropertyName("cabinPrices")]
        public List<CabinPrice> CabinPrices { get; set; }
    }

    public class SegmentInfo
    {
        [JsonPropertyName("airline")]
        public string Airline { get; set; }

        [JsonPropertyName("airlineName")]
        public string AirlineName { get; set; }

        [JsonPropertyName("flightNumber")]
        public string FlightNumber { get; set; }

        [JsonPropertyName("operatingAirline")]
        public string OperatingAirline { get; set; }

        [JsonPropertyName("operatingAirlineName")]
        public string OperatingAirlineName { get; set; }

        [JsonPropertyName("origin")]
        public string Origin { get; set; }

        [JsonPropertyName("originCity")]
        public string OriginCity { get; set; }

        [JsonPropertyName("destination")]
        public string Destination { get; set; }

        [JsonPropertyName("destinationCity")]
        public string DestinationCity { get; set; }

        [JsonPropertyName("departure")]
        public string Departure { get; set; }

        [JsonPropertyName("arrival")]
        public string Arrival { get; set; }

        [JsonPropertyName("duration")]
        public string Duration { get; set; }

        [JsonPropertyName("stopCount")]
        public int StopCount { get; set; }
    }

    public class CabinPrice
    {
        [JsonPropertyName("cabinName")]
        public string CabinName { get; set; }

        [JsonPropertyName("fareType")]
        public string FareType { get; set; }

        [JsonPropertyName("price")]
        public decimal Price { get; set; }

        [JsonPropertyName("currency")]
        public string Currency { get; set; }

        [JsonPropertyName("availableSeats")]
        public int AvailableSeats { get; set; }
    }

    #endregion
}
