using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
                .ConfigureFunctionsWebApplication()
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.AddJsonFile("appsettings.json", optional: false);
                    config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true);

                    if (context.HostingEnvironment.IsProduction())
                    {
                        var builtConfig = config.Build();
                        var keyVaultUrl = builtConfig["KeyVault:Url"];

                        //config.AddAzureKeyVault(
                        //    new Uri(keyVaultUrl),
                        //    new DefaultAzureCredential());
                    }

                    config.AddEnvironmentVariables();
                    config.AddCommandLine(args);
                })
                .ConfigureServices((context, services) =>
                {
                    services.Configure<ClaudeOptions>(context.Configuration.GetSection("Claude"));
                    services.Configure<ToolOptions>(context.Configuration.GetSection("Tools"));

                    services.AddHttpClient();

                    services.AddSingleton<IClaudeClient, ClaudeClient>();
                    services.AddSingleton<IToolRegistry, ToolRegistry>();
                    services.AddSingleton<IWeatherTool, WeatherTool>();
                    services.AddSingleton<IAgentOrchestrator, AgentOrchestrator>();

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