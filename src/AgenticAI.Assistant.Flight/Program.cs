using System.Text.Json;
using System.Text.Json.Serialization;
using AgenticAI.Assistant.Flight.Models;
using AgenticAI.Assistant.Functions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using AgenticAI.Assistant.LanguageModel;
using AgenticAI.Assistant.Models;
using AgenticAI.Assistant.Orchestrator;
using AgenticAI.Assistant.Tooling;
using AgenticAI.Assistant.Flight.Tools;

namespace AgenticAI.Assistant.Flight
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

                    services.Configure<ClaudeOptions>(context.Configuration.GetSection("Claude"));
                    services.Configure<ToolOptions>(context.Configuration.GetSection("Tools"));

                    // Register core services
                    services.AddSingleton<IClaudeClient, ClaudeClient>();
                    services.AddSingleton<IAgentOrchestrator, AgentOrchestrator>();
                    services.AddSingleton<IToolRegistry, ToolRegistry>();
                    services.AddSingleton<MessageFunction>();

                    // Tools
                    services.AddSingleton<ITool, WeatherTool>();
                    services.AddSingleton<ITool, FlightSearchTool>();

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