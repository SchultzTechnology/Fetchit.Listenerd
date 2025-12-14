using Microsoft.EntityFrameworkCore;
using Fetchit.WebPage.Data;
using Fetchit.WebPage.Models;

namespace Fetchit.WebPage.Services;

public class MqttConfigService
{
    private readonly MqttConfigContext _context;

    public MqttConfigService(MqttConfigContext context)
    {
        _context = context;
    }

    public async Task<MqttConfiguration?> GetLatestConfigurationAsync()
    {
        return await _context.MqttConfigurations
            .OrderByDescending(c => c.UpdatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> HasConfigurationAsync()
    {
        return await _context.MqttConfigurations.AnyAsync();
    }

    public async Task<MqttConfiguration> SaveConfigurationAsync(MqttSettingsViewModel settings)
    {
        var config = new MqttConfiguration
        {
            ConnectionSecret = settings.ConnectionSecret,
            BrokerPort = settings.BrokerPort,
            TopicSubscribe = settings.TopicSubscribe,
            TopicPublish = settings.TopicPublish,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.MqttConfigurations.Add(config);
        await _context.SaveChangesAsync();

        return config;
    }

    public async Task<MqttConfiguration?> GetConfigurationByIdAsync(int id)
    {
        return await _context.MqttConfigurations.FindAsync(id);
    }

    public async Task<MqttConfiguration> UpdateConfigurationAsync(int id, MqttSettingsViewModel settings)
    {
        var config = await _context.MqttConfigurations.FindAsync(id);
        if (config == null)
        {
            throw new InvalidOperationException($"Configuration with ID {id} not found.");
        }

        config.ConnectionSecret = settings.ConnectionSecret;
        config.BrokerPort = settings.BrokerPort;
        config.TopicSubscribe = settings.TopicSubscribe;
        config.TopicPublish = settings.TopicPublish;
        config.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return config;
    }

    public async Task<List<MqttConfiguration>> GetAllConfigurationsAsync()
    {
        return await _context.MqttConfigurations
            .OrderByDescending(c => c.UpdatedAt)
            .ToListAsync();
    }

    public async Task<bool> DeleteConfigurationAsync(int id)
    {
        var config = await _context.MqttConfigurations.FindAsync(id);
        if (config == null)
        {
            return false;
        }

        _context.MqttConfigurations.Remove(config);
        await _context.SaveChangesAsync();
        return true;
    }
}
