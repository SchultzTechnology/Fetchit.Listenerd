using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fetchit.Listenerd.Data;
using Fetchit.Listenerd.Models;
using Microsoft.EntityFrameworkCore;

namespace Fetchit.Cli.Services
{
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

        public async Task<MqttConfiguration> SaveConfigurationAsync(
            string connectionSecret, int brokerPort, string topicSubscribe, string topicPublish,
            string? otelAuthToken = null)
        {
            var config = new MqttConfiguration
            {
                ConnectionSecret = connectionSecret,
                BrokerPort       = brokerPort,
                TopicSubscribe   = topicSubscribe,
                TopicPublish     = topicPublish,
                OtelAuthToken    = otelAuthToken,
                CreatedAt        = DateTime.UtcNow,
                UpdatedAt        = DateTime.UtcNow
            };

            _context.MqttConfigurations.Add(config);
            await _context.SaveChangesAsync();
            return config;
        }

        public async Task<MqttConfiguration> UpdateConfigurationAsync(
            int id, string connectionSecret, int brokerPort, string topicSubscribe, string topicPublish,
            string? otelAuthToken = null)
        {
            var config = await _context.MqttConfigurations.FindAsync(id);
            if (config == null)
                throw new InvalidOperationException($"Configuration with ID {id} not found.");

            config.ConnectionSecret = connectionSecret;
            config.BrokerPort       = brokerPort;
            config.TopicSubscribe   = topicSubscribe;
            config.TopicPublish     = topicPublish;
            config.OtelAuthToken    = otelAuthToken;
            config.UpdatedAt        = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return config;
        }

        public async Task<bool> DeleteConfigurationAsync(int id)
        {
            var config = await _context.MqttConfigurations.FindAsync(id);
            if (config == null)
                return false;

            _context.MqttConfigurations.Remove(config);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<MqttConfiguration>> GetAllConfigurationsAsync()
        {
            return await _context.MqttConfigurations
                .OrderByDescending(c => c.UpdatedAt)
                .ToListAsync();
        }

        public async Task<MqttConfiguration?> GetConfigurationByIdAsync(int id)
        {
            return await _context.MqttConfigurations.FindAsync(id);
        }
    }
}
