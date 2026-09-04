using System;
using System.Threading.Tasks;
using Fetchit.Cli.Services;
using Fetchit.Listenerd.Data;
using Microsoft.EntityFrameworkCore;

namespace Fetchit.Cli.Commands
{
    public class ShowCommand
    {
        private readonly MqttConfigContext      _context;
        private readonly MqttConfigService      _configService;
        private readonly ConnectionSecretService _secretService;

        public ShowCommand(MqttConfigContext context)
        {
            _context       = context;
            _configService = new MqttConfigService(context);
            _secretService = new ConnectionSecretService();
        }

        public async Task<int> RunAsync()
        {
            try
            {
                _context.Database.EnsureCreated();

                var config = await _configService.GetLatestConfigurationAsync();

                if (config == null)
                {
                    Console.WriteLine("No configuration found in database.");
                    Console.WriteLine("Run with --connection-secret to save a configuration first.");
                    return 1;
                }

                Console.WriteLine("==========================================");
                Console.WriteLine("  Saved MQTT Configuration");
                Console.WriteLine("==========================================");
                Console.WriteLine($"  ID           : {config.Id}");
                Console.WriteLine($"  Broker Port  : {config.BrokerPort}");
                Console.WriteLine($"  Topic Sub    : {config.TopicSubscribe}");
                Console.WriteLine($"  Topic Pub    : {config.TopicPublish}");
                Console.WriteLine($"  OTel Token   : {(string.IsNullOrEmpty(config.OtelAuthToken) ? "(not set)" : new string('*', config.OtelAuthToken.Length))}");
                Console.WriteLine($"  Created At   : {config.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
                Console.WriteLine($"  Updated At   : {config.UpdatedAt:yyyy-MM-dd HH:mm:ss} UTC");
                Console.WriteLine();

                Console.WriteLine("  Connection Secret (decoded):");
                var secret = _secretService.DecodeConnectionSecret(config.ConnectionSecret);
                if (secret != null)
                {
                    Console.WriteLine($"    Broker     : {secret.Broker}");
                    Console.WriteLine($"    ClientId   : {secret.ClientId}");
                    Console.WriteLine($"    LocationId : {secret.LocationId}");
                    Console.WriteLine($"    UserName   : {secret.UserName}");
                    Console.WriteLine($"    Password   : {"*".PadRight(secret.Password.Length, '*')}");
                }
                else
                {
                    Console.WriteLine("    (Could not decode - may be invalid Base64)");
                    Console.WriteLine($"    Raw Value: {config.ConnectionSecret}");
                }

                Console.WriteLine("==========================================");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading configuration: {ex.Message}");
                return 1;
            }
        }
    }
}
