using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TestAiAgent.LanguageModel;
using TestAiAgent.Models;
using TestAiAgent.Orchestrator;
using TestAiAgent.Tooling;
using TestAiAgent.Tooling.Tools;

namespace TestAiAgent
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
                    config.AddJsonFile("appsettings.json", optional: false);
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
                    services.AddApplicationServices(context.Configuration);
                    services.AddHttpClient();
                    
                    services.Configure<ClaudeOptions>(context.Configuration.GetSection("Claude"));
                    services.Configure<ToolOptions>(context.Configuration.GetSection("Tools"));

                    services.AddSingleton<IClaudeClient, ClaudeClient>();
                    services.AddSingleton<IAgentOrchestrator, AgentOrchestrator>();
                    services.AddSingleton<IToolRegistry, ToolRegistry>();

                    // Tools
                    services.AddSingleton<IWeatherTool, WeatherTool>();
                    services.AddSingleton<IFlightSearchTool, FlightSearchTool>();

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
}
