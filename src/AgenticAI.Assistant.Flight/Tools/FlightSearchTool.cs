using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgenticAI.Assistant.Flight.Models;
using AgenticAI.Assistant.Models;
using AgenticAI.Assistant.Tooling;

namespace AgenticAI.Assistant.Flight.Tools
{
    /// <summary>
    /// Flight search tool implementation for retrieving real-time flight availability
    /// </summary>
    public class FlightSearchTool : ITool
    {
        private readonly HttpClient _httpClient;
        private readonly FlightSearchToolOptions _options;
        private readonly ILogger<FlightSearchTool> _logger;

        public FlightSearchTool(
            IHttpClientFactory httpClientFactory,
            IOptions<ToolOptions> options,
            ILogger<FlightSearchTool> logger)
        {
            _httpClient = httpClientFactory.CreateClient();
            _options = options.Value.FlightSearch;
            _logger = logger;

            // Set up HTTP client
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        }

        /// <summary>
        /// Tool definition for Claude
        /// </summary>
        public Tool Definition => new Tool
        {
            Name = "search_flights",
            Description = "Search for available flights between two airports on specified dates.",
            InputSchema = new InputSchema
            {
                Type = "object",
                Properties = new Dictionary<string, SchemaProperty>
                {
                    {
                        "origin", new SchemaProperty
                        {
                            Type = "string",
                            Description = "Origin airport code (e.g., 'LHR' for London Heathrow)"
                        }
                    },
                    {
                        "destination", new SchemaProperty
                        {
                            Type = "string",
                            Description = "Destination airport code (e.g., 'JFK' for New York JFK)"
                        }
                    },
                    {
                        "departureDate", new SchemaProperty
                        {
                            Type = "string",
                            Description = "Departure date in YYYY-MM-DD format (e.g., '2025-09-22'). Must be current or future date."
                        }
                    },
                    {
                        "returnDate", new SchemaProperty
                        {
                            Type = "string",
                            Description = "Return date in YYYY-MM-DD format (optional), this must be after the departure date."
                        }
                    },
                    {
                        "nonStopOnly", new SchemaProperty
                        {
                            Type = "boolean",
                            Description = "If true, search only for non-stop flights (default: false)"
                        }
                    }
                },
                Required = new List<string> { "origin", "destination", "departureDate" }
            }
        };

        /// <summary>
        /// Executes the flight search tool to get flight availability data
        /// </summary>
        public async Task<string> ExecuteAsync(JsonElement input)
        {
            try
            {
                // Parse input parameters
                var origin = input.GetProperty("origin").GetString().ToUpper();
                var destination = input.GetProperty("destination").GetString().ToUpper();
                var departureDateStr = input.GetProperty("departureDate").GetString();

                // Parse and validate departure date
                if (!DateTime.TryParse(departureDateStr, out var parsedDepartureDate))
                {
                    return JsonSerializer.Serialize(new
                    {
                        success = false,
                        error = $"Invalid departure date format: {departureDateStr}. Please use YYYY-MM-DD format."
                    });
                }

                // Check if departure date is in the past
                if (parsedDepartureDate.Date < DateTime.Today)
                {
                    return JsonSerializer.Serialize(new
                    {
                        success = false,
                        error = $"Departure date ({departureDateStr}) is in the past. Please provide a current or future date.",
                        currentDate = DateTime.Today.ToString("yyyy-MM-dd"),
                        tomorrow = DateTime.Today.AddDays(1).ToString("yyyy-MM-dd")
                    });
                }

                var departureDate = parsedDepartureDate.ToString("yyyy-MM-ddT00:00:00");

                // Optional parameters
                var hasReturnDate = input.TryGetProperty("returnDate", out var returnDateElement);
                DateTime? parsedReturnDate = null;
                string returnDate = null;

                if (hasReturnDate && !string.IsNullOrEmpty(returnDateElement.GetString()))
                {
                    if (!DateTime.TryParse(returnDateElement.GetString(), out var tempReturnDate))
                    {
                        return JsonSerializer.Serialize(new
                        {
                            success = false,
                            error = $"Invalid return date format: {returnDateElement}. Please use YYYY-MM-DD format."
                        });
                    }

                    parsedReturnDate = tempReturnDate;

                    // Validate return date is after departure date
                    if (parsedReturnDate < parsedDepartureDate)
                    {
                        return JsonSerializer.Serialize(new
                        {
                            success = false,
                            error = $"Return date ({returnDateElement}) must be after departure date ({departureDateStr})."
                        });
                    }

                    returnDate = parsedReturnDate.Value.ToString("yyyy-MM-ddT00:00:00");
                }

                var nonStopOnly = false;
                if (input.TryGetProperty("nonStopOnly", out var nonStopElement))
                {
                    nonStopOnly = nonStopElement.GetBoolean();
                }

                // Build the search request
                var searchOriginDestinations = new List<object>
                {
                    new
                    {
                        origin,
                        destination,
                        departureDate,
                        connectionAirports = (string)null
                    }
                };

                // Add return flight if provided
                if (!string.IsNullOrEmpty(returnDate))
                {
                    searchOriginDestinations.Add(new
                    {
                        origin = destination,
                        destination = origin,
                        departureDate = returnDate,
                        connectionAirports = (string)null
                    });
                }

                // Create the GraphQL request
                var request = new
                {
                    operationName = "SearchOffers",
                    variables = new
                    {
                        request = new
                        {
                            flightSearchRequest = new
                            {
                                searchOriginDestinations,
                                bundleOffer = false,
                                flexiDateSearch = false,
                                calendarSearch = false,
                                nonStopOnly,
                                refundableOnly = false,
                                checkInBaggageAllowance = true,
                                carryOnBaggageAllowance = true,
                                offerID = "",
                                currentTripIndexId = "0",
                                cabinTypes = new string[] { },
                                fareFamilies = new string[] { },
                                awardSearch = false,
                                promoCode = ""
                            },
                            customerDetails = new[]
                            {
                                new
                                {
                                    custId = "ADULT_1",
                                    ptc = "ADT"
                                }
                            }
                        }
                    },
                    query = @"query SearchOffers($request: FlightOfferRequestInput!) {
                      searchOffers(request: $request) {
                        result {
                          slices {
                            current
                            total
                          }
                          criteria {
                            origin {
                              code
                            }
                            destination {
                              code
                            }
                            departing
                          }
                          slice {
                            flightsAndFares {
                              flight {
                                segments {
                                  flightNumber
                                  operatingFlightNumber
                                  origin {
                                    code
                                  }
                                  destination {
                                    code
                                  }
                                }
                                duration
                                origin {
                                  code
                                }
                                destination {
                                  code
                                }
                                departure
                                arrival
                              }
                              fares {
                                availability
                                id
                                price {
                                  amountIncludingTax
                                  currency
                                }
                                fareSegments {
                                  cabinName
                                }
                                fareFamilyType
                              }
                            }
                          }
                          basketId
                        }
                      }
                    }"
                };

                // Convert request to JSON
                var jsonContent = JsonSerializer.Serialize(request);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // Send the request to the GraphQL endpoint
                _logger.LogInformation($"Sending flight search request to {_options.ApiEndpoint}");
                var response = await _httpClient.PostAsync(_options.ApiEndpoint, content);

                // Ensure success
                response.EnsureSuccessStatusCode();
                var responseContent = await response.Content.ReadAsStringAsync();

                // Parse and convert the response using deserialization
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };

                var searchResponse = JsonSerializer.Deserialize<FlightSearchGraphQLResponse>(responseContent, jsonOptions);

                if (searchResponse?.Data?.SearchOffers?.Result?.Slice?.FlightsAndFares == null ||
                    searchResponse.Data.SearchOffers.Result.Slice.FlightsAndFares.Count == 0)
                {
                    return JsonSerializer.Serialize(new
                    {
                        success = false,
                        error = "No flights found for the specified criteria."
                    });
                }

                var criteria = searchResponse.Data.SearchOffers.Result.Criteria;
                var flights = searchResponse.Data.SearchOffers.Result.Slice.FlightsAndFares;

                // Transform the flight data into our response model
                var flightResults = new List<FlightResult>();

                foreach (var flightFare in flights)
                {
                    var flight = flightFare.Flight;
                    var segments = new List<SegmentInfo>();

                    foreach (var segment in flight.Segments)
                    {
                        segments.Add(new SegmentInfo
                        {
                            Airline = segment.Airline?.Code,
                            AirlineName = segment.Airline?.Name,
                            FlightNumber = segment.FlightNumber,
                            OperatingAirline = segment.OperatingAirline?.Code,
                            OperatingAirlineName = segment.OperatingAirline?.Name,
                            Origin = segment.Origin.Code,
                            OriginCity = segment.Origin.CityName,
                            Destination = segment.Destination.Code,
                            DestinationCity = segment.Destination.CityName,
                            Departure = segment.Departure,
                            Arrival = segment.Arrival,
                            Duration = segment.Duration,
                            StopCount = segment.StopCount
                        });
                    }

                    var cabinPrices = new List<CabinPrice>();

                    foreach (var fare in flightFare.Fares)
                    {
                        // Skip fares with null price or no available seats
                        if (fare.Price == null || fare.FareSegments == null || fare.FareSegments.Count == 0)
                            continue;

                        cabinPrices.Add(new CabinPrice
                        {
                            CabinName = fare.FareSegments[0].CabinName,
                            FareType = fare.FareFamilyType,
                            Price = fare.Price?.AmountIncludingTax ?? 0,
                            Currency = fare.Price?.Currency,
                            AvailableSeats = fare.AvailableSeatCount ?? 0
                        });
                    }

                    flightResults.Add(new FlightResult
                    {
                        Origin = flight.Origin?.Code,
                        OriginCity = flight.Origin?.CityName,
                        Destination = flight.Destination?.Code,
                        DestinationCity = flight.Destination?.CityName,
                        Departure = flight.Departure,
                        Arrival = flight.Arrival,
                        Duration = flight.Duration,
                        Segments = segments,
                        CabinPrices = cabinPrices,
                        IsNonStop = flight.Segments?.Count == 1
                    });
                }

                // Create the search results response
                var searchResults = new FlightSearchResults
                {
                    Success = true,
                    Flights = flightResults,
                    SearchCriteria = new SearchCriteria
                    {
                        Origin = criteria.Origin?.Code,
                        OriginCity = criteria.Origin?.CityName,
                        Destination = criteria.Destination?.Code,
                        DestinationCity = criteria.Destination?.CityName,
                        DepartureDate = departureDateStr,
                        ReturnDate = parsedReturnDate?.ToString("yyyy-MM-dd"),
                        NonStopOnly = nonStopOnly
                    },
                    FlightCount = flightResults.Count
                };

                // Return the serialized response
                return JsonSerializer.Serialize(searchResults, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing flight search tool");
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Failed to retrieve flight data: {ex.Message}"
                });
            }
        }

        #region Response Models

        // Main response model
        private class FlightSearchGraphQLResponse
        {
            public GraphQLData Data { get; set; }
        }

        private class GraphQLData
        {
            public SearchOffersData SearchOffers { get; set; }
        }

        private class SearchOffersData
        {
            public ResultData Result { get; set; }
        }

        private class ResultData
        {
            public SlicesInfo Slices { get; set; }
            public CriteriaData Criteria { get; set; }
            public SliceData Slice { get; set; }
        }

        private class SlicesInfo
        {
            public int Current { get; set; }
            public int Total { get; set; }
        }

        private class CriteriaData
        {
            public LocationInfo Origin { get; set; }
            public LocationInfo Destination { get; set; }
            public string Departing { get; set; }
        }

        private class SliceData
        {
            public List<FlightAndFare> FlightsAndFares { get; set; }
        }

        private class FlightAndFare
        {
            public FlightData Flight { get; set; }
            public List<FareData> Fares { get; set; }
        }

        private class FlightData
        {
            public List<SegmentData> Segments { get; set; }
            public string Duration { get; set; }
            public LocationInfo Origin { get; set; }
            public LocationInfo Destination { get; set; }
            public string Departure { get; set; }
            public string Arrival { get; set; }
        }

        private class SegmentData
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

        private class AirlineInfo
        {
            public string Code { get; set; }
            public string Name { get; set; }
        }

        private class LocationInfo
        {
            public string Code { get; set; }
            public string CityName { get; set; }
            public string CountryName { get; set; }
            public string AirportName { get; set; }
        }

        private class FareData
        {
            public int? AvailableSeatCount { get; set; }
            public string FareFamilyType { get; set; }
            public PriceData Price { get; set; }
            public List<FareSegmentData> FareSegments { get; set; }
        }

        private class PriceData
        {
            public decimal AmountIncludingTax { get; set; }
            public string Currency { get; set; }
        }

        private class FareSegmentData
        {
            public string CabinName { get; set; }
        }

        #endregion

        #region Response Output Models

        private class FlightSearchResults
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

        private class SearchCriteria
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

        private class FlightResult
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

        private class SegmentInfo
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

        private class CabinPrice
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
}