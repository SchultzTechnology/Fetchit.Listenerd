using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using System.Text;
using System.Text.Json;

namespace Fetchit.Listenerd.Service;

public class MQTTClient
{
    public class MqttService
    {
        private readonly ILogger<MqttService> _logger;
        private readonly MqttConfigService _configService;

        private IMqttClient _mqttClient;
        private MqttClientOptions _options;

        private string _topicPublish;
        private bool _isInitialized = false;

        public MqttService(ILogger<MqttService> logger, MqttConfigService configService)
        {
            _logger = logger;
            _configService = configService;
        }

        public async Task InitializeAsync()
        {
            try
            {
                // Get configuration from database
                var config = await _configService.GetLatestConfigurationAsync();
                if (config == null)
                {
                    _logger.LogError("No MQTT configuration found in database. Please configure MQTT settings in the web interface.");
                    return;
                }

                // Decode the connection secret
                var connectionDetails = _configService.DecodeConnectionSecret(config.ConnectionSecret);
                if (connectionDetails == null)
                {
                    _logger.LogError("Failed to decode connection secret. Please verify the configuration.");
                    return;
                }

                _topicPublish = config.TopicPublish;

                string broker = connectionDetails.Broker;
                int port = config.BrokerPort;
                string clientId = connectionDetails.ClientId;
                string username = connectionDetails.UserName;
                string password = connectionDetails.Password;

                _logger.LogInformation("Initializing MQTT with configuration from database:");
                _logger.LogInformation("  Broker: {Broker}", broker);
                _logger.LogInformation("  Port: {Port}", port);
                _logger.LogInformation("  ClientId: {ClientId}", clientId);
                _logger.LogInformation("  Topic Publish: {TopicPublish}", _topicPublish);

                var factory = new MqttFactory();
                _mqttClient = factory.CreateMqttClient();
                
                _options = new MqttClientOptionsBuilder()
                   .WithClientId(clientId)
                    .WithWebSocketServer(broker)
                    .WithCredentials(username, password)
                    .WithCleanSession()
                    .Build();         

                _mqttClient.ConnectedAsync += async e =>
                {
                    _logger.LogInformation("MQTT Connected (v3.1.1).");
                    _isInitialized = true;
                };

                _mqttClient.DisconnectedAsync += async e =>
                {
                    _logger.LogWarning("MQTT Disconnected. Reconnecting...");
                    _isInitialized = false;
                    await Task.Delay(2000);
                    await ConnectAsync();
                };

                await ConnectAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing MQTT client");
            }
        }

        private async Task ConnectAsync()
        {
            try
            {
                if (_mqttClient != null && _options != null)
                {
                    await _mqttClient.ConnectAsync(_options);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MQTT Connection error");
            }
        }

      
        public async Task PublishSipAsync(string src, string dest, string sipData)
        {
            if (_mqttClient == null || !_mqttClient.IsConnected)
            {
                if (!_isInitialized)
                {
                    _logger.LogWarning("MQTT client not initialized. Attempting to initialize...");
                    await InitializeAsync();
                }
                return;
            }

            var payload = JsonSerializer.Serialize(new
            {
                source_ip = src,
                destination_ip = dest,
                sip_data = sipData,
                timestamp = DateTime.UtcNow
            });

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(_topicPublish)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            await _mqttClient.PublishAsync(message);

            _logger.LogInformation($"Published SIP Packet → {src} → {dest}");
        }
    }
}