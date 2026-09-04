using System;
using System.Threading.Tasks;
using Fetchit.Cli.Services;
using Fetchit.Listenerd.Data;
using Microsoft.EntityFrameworkCore;

namespace Fetchit.Cli.Commands
{
    public class SaveCommand
    {
        private readonly MqttConfigContext _context;
        private readonly string[] _args;

        private readonly MqttConfigService  _configService;
        private readonly SupervisorService  _supervisorService;

        public SaveCommand(MqttConfigContext context, string[] args)
        {
            _context           = context;
            _args              = args;
            _configService     = new MqttConfigService(context);
            _supervisorService = new SupervisorService();
        }

        public async Task<int> RunAsync()
        {

            var connectionSecret = GetArg("--connection-secret");
            var portStr          = GetArg("--port");
            var topicSub         = GetArg("--topic-sub");
            var topicPub         = GetArg("--topic-pub");
            var otelToken        = GetArg("--otel-token");

            bool hasError = false;

            if (string.IsNullOrWhiteSpace(connectionSecret))
            {
                Console.WriteLine("Error: --connection-secret is required.");
                hasError = true;
            }

            if (string.IsNullOrWhiteSpace(portStr))
            {
                Console.WriteLine("Error: --port is required.");
                hasError = true;
            }
            else if (!int.TryParse(portStr, out int parsedPort) || parsedPort < 1 || parsedPort > 65535)
            {
                Console.WriteLine("Error: --port must be a number between 1 and 65535.");
                hasError = true;
            }

            if (string.IsNullOrWhiteSpace(topicSub))
            {
                Console.WriteLine("Error: --topic-sub is required.");
                hasError = true;
            }

            if (string.IsNullOrWhiteSpace(topicPub))
            {
                Console.WriteLine("Error: --topic-pub is required.");
                hasError = true;
            }

            if (hasError)
            {
                PrintUsage();
                return 1;
            }

            int port = int.Parse(portStr!);

            try
            {
                _context.Database.EnsureCreated();

                var existingConfig = await _configService.GetLatestConfigurationAsync();

                if (existingConfig != null)
                {
                    await _configService.UpdateConfigurationAsync(
                        existingConfig.Id, connectionSecret!, port, topicSub!, topicPub!, otelToken);
                    Console.WriteLine("MQTT configuration updated successfully.");
                }
                else
                {
                    await _configService.SaveConfigurationAsync(
                        connectionSecret!, port, topicSub!, topicPub!, otelToken);
                    Console.WriteLine("MQTT configuration saved successfully.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving to database: {ex.Message}");
                if (ex.InnerException != null)
                    Console.WriteLine($"Inner Error: {ex.InnerException.Message}");
                return 1;
            }

            Console.WriteLine("Restarting Listenerd service...");
            await _supervisorService.RestartListenerdAsync();

            Console.WriteLine();
            Console.WriteLine("==========================================");
            Console.WriteLine("  Setup complete!");
            Console.WriteLine("==========================================");
            return 0;
        }

        static void PrintUsage()
        {
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  Save configuration:");
            Console.WriteLine("    dotnet Fetchit.Cli.dll \\");
            Console.WriteLine("      --connection-secret \"<base64-string>\" \\");
            Console.WriteLine("      --port <1-65535> \\");
            Console.WriteLine("      --topic-sub \"fetchit/commands/#\" \\");
            Console.WriteLine("      --topic-pub \"fetchit/status/\" \\");
            Console.WriteLine("      [--otel-token \"<signoz-auth-token>\"]");
            Console.WriteLine();
            Console.WriteLine("  View saved configuration:");
            Console.WriteLine("    dotnet Fetchit.Cli.dll --show");
            Console.WriteLine();
            Console.WriteLine("  Delete saved configuration:");
            Console.WriteLine("    dotnet Fetchit.Cli.dll --delete");
            Console.WriteLine();
            Console.WriteLine("Optional:");
            Console.WriteLine("  --db \"<path-to-mqttconfig.db>\"");
            Console.WriteLine("  --otel-token \"<signoz-auth-token>\"");
        }

        string? GetArg(string key)
        {
            var index = Array.IndexOf(_args, key);
            if (index >= 0 && index < _args.Length - 1)
                return _args[index + 1];
            return null;
        }
    }
}
