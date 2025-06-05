using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgenticAI.McpServer.FlightSearch
{
    public class SearchClient : ISearchClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<SearchClient> _logger;
        private readonly FlightSearchOptions _options;

        public SearchClient(
            IHttpClientFactory httpClientFactory,
            IOptions<FlightSearchOptions> options,
            ILogger<SearchClient> logger)
        {
            _httpClient = httpClientFactory.CreateClient();
            _options = options.Value;
            _logger = logger;

            // Set up HTTP client
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        }

        /// <summary>
        /// Executes the flight search tool to get flight availability data
        /// </summary>
        public async Task<string> ExecuteAsync(SearchRequest searchRequest)
        {
            try
            {  
                // Check if departure date is in the past
                if (searchRequest.DepartureDate.Date < DateTime.Today)
                {
                    return JsonSerializer.Serialize(new
                    {
                        success = false,
                        error = $"Departure date ({searchRequest.DepartureDate}) is in the past. Please provide a current or future date.",
                        currentDate = DateTime.Today.ToString("yyyy-MM-dd"),
                        tomorrow = DateTime.Today.AddDays(1).ToString("yyyy-MM-dd")
                    });
                }

                //var departureDate = searchRequest.DepartureDate.ToString("yyyy-MM-ddT00:00:00");

                //// Optional parameters
                //var hasReturnDate = input.TryGetProperty("returnDate", out var returnDateElement);
                //DateTime? parsedReturnDate = null;
                //string returnDate = null;

                //if (hasReturnDate && !string.IsNullOrEmpty(returnDateElement.GetString()))
                //{
                //    if (!DateTime.TryParse(returnDateElement.GetString(), out var tempReturnDate))
                //    {
                //        return JsonSerializer.Serialize(new
                //        {
                //            success = false,
                //            error = $"Invalid return date format: {returnDateElement}. Please use YYYY-MM-DD format."
                //        });
                //    }

                //    parsedReturnDate = tempReturnDate;

                //    // Validate return date is after departure date
                //    if (parsedReturnDate < parsedDepartureDate)
                //    {
                //        return JsonSerializer.Serialize(new
                //        {
                //            success = false,
                //            error = $"Return date ({returnDateElement}) must be after departure date ({departureDateStr})."
                //        });
                //    }

                //    returnDate = parsedReturnDate.Value.ToString("yyyy-MM-ddT00:00:00");
                //}

                var nonStopOnly = false;
                //if (input.TryGetProperty("nonStopOnly", out var nonStopElement))
                //{
                //    nonStopOnly = nonStopElement.GetBoolean();
                //}

                // Build the search request
                var searchOriginDestinations = new List<object>
                {
                    new
                    {
                        searchRequest.Origin,
                        searchRequest.Destination,
                        searchRequest.DepartureDate,
                        connectionAirports = (string)null
                    }
                };

                //// Add return flight if provided
                //if (!string.IsNullOrEmpty(returnDate))
                //{
                //    searchOriginDestinations.Add(new
                //    {
                //        origin = destination,
                //        destination = origin,
                //        departureDate = returnDate,
                //        connectionAirports = (string)null
                //    });
                //}

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
                        DepartureDate = searchRequest.DepartureDate.ToString(),
                        //ReturnDate = parsedReturnDate?.ToString("yyyy-MM-dd"),
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
    }
}