using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgenticAI.McpServer.FlightSearch
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var host = new HostBuilder()
                .ConfigureLogging((context, logging) =>
                {
                    logging.ClearProviders(); // Remove all default providers including Console
                    logging.AddDebug(); // Add only Debug provider
                    logging.SetMinimumLevel(LogLevel.Debug);
                })
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.AddJsonFile("appsettings.json", optional: true);
                    //config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true);

                    if (context.HostingEnvironment.IsProduction())
                    {
                        //var builtConfig = config.Build();
                        //var keyVaultUrl = builtConfig["KeyVault:Url"];

                        //config.AddAzureKeyVault(
                        //    new Uri(keyVaultUrl),
                        //    new DefaultAzureCredential());
                    }

                    config.AddEnvironmentVariables();
                    config.AddCommandLine(args);
                    config.Build();
                })
                .ConfigureFunctionsWebApplication()
                .ConfigureServices((context, services) =>
                {
                    //services.AddApplicationServices(context.Configuration);
                    services.AddHttpClient();

                    services.Configure<FlightSearchOptions>(context.Configuration.GetSection("FlightSearch"));

                    services.AddSingleton<ISearchClient, SearchClient>();

                    services.Configure<JsonSerializerOptions>(options =>
                    {
                        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                    });
                })
                .Build();

            host.Run();
        }
    }

    public class FlightSearchOptions
    {
        public string ApiEndpoint { get; set; }
    }
}