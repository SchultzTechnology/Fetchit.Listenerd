using System.Text.RegularExpressions;
using Fetchit.Listenerd.Data;
using Fetchit.Listenerd.Models;
using Fetchit.Listenerd.Service;
using Microsoft.EntityFrameworkCore;
using MQTTnet;
using MQTTnet.Client;

public class MQTTClient
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MQTTClient> _logger;
    private MqttClientOptions _mqttOptions;
    private IMqttClient _mqttClient;
    private MqttConfiguration _mqttConfiguration;
    private ConnectionSecretDto _connectionSecret;
    private bool _connected;

    public MQTTClient(ILogger<MQTTClient> logger, IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task LoadMqttSettingsAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<MqttConfigContext>();

            _mqttConfiguration = await context.MqttConfigurations
                .OrderByDescending(c => c.UpdatedAt)
                .FirstOrDefaultAsync()
                ?? throw new InvalidOperationException("No MQTT configuration found.");

            _connectionSecret = System.Text.Json.JsonSerializer.Deserialize<ConnectionSecretDto>(_mqttConfiguration.ConnectionSecret)
                ?? throw new InvalidOperationException("Invalid connection secret.");
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Error configuring MQTT client");
            throw;
        }
    }

    public async Task InitializeMqttBrokerAsync()
    {
        try
        {
            var _clientId = "FetchitListenerdClient_" + _connectionSecret.ClientId;

            _logger.LogInformation("Initializing MQTT client");
            _logger.LogInformation("    MQTT Broker: {Broker}", _connectionSecret.Broker);
            _logger.LogInformation("    MQTT ClientId: {ClientId}", _clientId);
            _logger.LogInformation("    MQTT LocationId: {LocationId}", _connectionSecret.LocationId);
            _logger.LogInformation("    MQTT Username: {Username}", _connectionSecret.UserName);
            _logger.LogInformation("    MQTT Password: {Password}", _connectionSecret.Password);
            _logger.LogInformation("    MQTT BrokerPort: {BrokerPort}", _mqttConfiguration.BrokerPort);
            _logger.LogInformation("    MQTT TopicSubscribe: {TopicSubscribe}", _mqttConfiguration.TopicSubscribe);
            _logger.LogInformation("    MQTT TopicPublish: {TopicPublish}", _mqttConfiguration.TopicPublish);

            var factory = new MqttFactory();
            _mqttClient = factory.CreateMqttClient();

            if (_connectionSecret.Broker.StartsWith("ws"))
            {
                _mqttOptions = new MqttClientOptionsBuilder()
                    .WithClientId(_clientId)
                    .WithWebSocketServer(_connectionSecret.Broker)
                    .WithCredentials(_connectionSecret.UserName, _connectionSecret.Password)
                    .WithCleanSession()
                    .Build();
            }
            else
            {
                _mqttOptions = new MqttClientOptionsBuilder()
                    .WithClientId(_clientId)
                    .WithTcpServer(_connectionSecret.Broker, _mqttConfiguration.BrokerPort)
                    .WithCredentials(_connectionSecret.UserName, _connectionSecret.Password)
                    .WithCleanSession()
                    .Build();
            }

            _mqttClient.ConnectedAsync += async e =>
            {
                _connected = true;
                _logger.LogInformation("MQTT Connected (v3.1.1).");
            };

            _mqttClient.DisconnectedAsync += async e =>
            {
                _connected = false;
                _logger.LogWarning("MQTT Disconnected. Reconnecting...");
                await Task.Delay(2000);
                try
                {
                    if (_mqttClient != null && _mqttOptions != null)
                        await _mqttClient.ConnectAsync(_mqttOptions);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "MQTT Connection error");
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing MQTT client");
        }
    }


    public async Task PublishSipAsync(Fetchit.Listenerd.SipPacket packet)
    {
        if (_mqttClient == null)
        {
            _logger.LogWarning("MQTT client is not initialized.");
            return;
        }

        if (!_mqttClient.IsConnected)
        {
            _logger.LogWarning("MQTT client is not connected.");
            return;
        }

        if (!_connected)
        {
            _logger.LogWarning("MQTT client is not connected (internal flag).");
            return;
        }

        // Use the rich properties parsed by the class
        var payload = new
        {
            Source = packet.SourceIp,
            Destination = packet.DestinationIp,
            Caller = packet.CallerName,
            Extension = packet.Number,
            IsInvite = packet.IsInvite,
            FromHeader = packet.FromRaw,
            CSeq = packet.CSeqRaw,
            Timestamp = DateTime.UtcNow,
            PacketCount = packet.TotalPacketsReceived
        };

        string jsonPayload = System.Text.Json.JsonSerializer.Serialize(payload);

        // Log based on invite status
        if (packet.IsInvite)
        {
            _logger.LogInformation(
                "Incoming INVITE: {Caller} ({Extension}) from {SourceIp} - Packet #{PacketCount}",
                packet.CallerName,
                packet.Number,
                packet.SourceIp,
                packet.TotalPacketsReceived);
        }
        else
        {
            _logger.LogDebug(
                "SIP message from {SourceIp} to {DestinationIp} - Packet #{PacketCount}",
                packet.SourceIp,
                packet.DestinationIp,
                packet.TotalPacketsReceived);
        }

        // Publish to MQTT broker
        try
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(_mqttConfiguration.TopicPublish)
                .WithPayload(jsonPayload)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .WithRetainFlag(false)
                .Build();

            await _mqttClient.PublishAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish SIP message to MQTT");
        }
    }

}