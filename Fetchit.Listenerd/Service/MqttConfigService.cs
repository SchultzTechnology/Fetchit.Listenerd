using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Fetchit.Listenerd.Data;
using Fetchit.Listenerd.Models;

namespace Fetchit.Listenerd.Service;

public class MqttConfigService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MqttConfigService> _logger;

    public MqttConfigService(IServiceProvider serviceProvider, ILogger<MqttConfigService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<MqttConfiguration?> GetLatestConfigurationAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<MqttConfigContext>();
            
            return await context.MqttConfigurations
                .OrderByDescending(c => c.UpdatedAt)
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving MQTT configuration from database");
            return null;
        }
    }

    public ConnectionSecretDto? DecodeConnectionSecret(string base64Secret)
    {
        try
        {
            var jsonBytes = Convert.FromBase64String(base64Secret);
            var jsonString = Encoding.UTF8.GetString(jsonBytes);
            return JsonSerializer.Deserialize<ConnectionSecretDto>(jsonString);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error decoding connection secret");
            return null;
        }
    }
}
