using System;
using System.Threading.Tasks;
using Fetchit.Cli.Services;
using Fetchit.Listenerd.Data;
using Microsoft.EntityFrameworkCore;

namespace Fetchit.Cli.Commands
{
    public class DeleteCommand
    {
        private readonly MqttConfigContext _context;
        private readonly MqttConfigService _configService;
        private readonly SupervisorService _supervisorService;

        public DeleteCommand(MqttConfigContext context)
        {
            _context           = context;
            _configService     = new MqttConfigService(context);
            _supervisorService = new SupervisorService();
        }

        public async Task<int> RunAsync()
        {
            try
            {
                _context.Database.EnsureCreated();
                _context.EnsureSchemaUpToDate();

                var config = await _configService.GetLatestConfigurationAsync();

                if (config == null)
                {
                    Console.WriteLine("Error: No configuration to delete.");
                    return 1;
                }

                var result = await _configService.DeleteConfigurationAsync(config.Id);

                if (result)
                {
                    Console.WriteLine("MQTT configuration deleted successfully.");

                    Console.WriteLine("Restarting Listenerd service...");
                    await _supervisorService.RestartListenerdAsync();

                    Console.WriteLine();
                    Console.WriteLine("==========================================");
                    Console.WriteLine("  Configuration deleted and service restarted!");
                    Console.WriteLine("==========================================");
                    return 0;
                }
                else
                {
                    Console.WriteLine("Error: Configuration not found.");
                    return 1;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting configuration: {ex.Message}");
                if (ex.InnerException != null)
                    Console.WriteLine($"Inner Error: {ex.InnerException.Message}");
                return 1;
            }
        }
    }
}
