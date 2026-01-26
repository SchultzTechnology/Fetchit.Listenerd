using System.Text.RegularExpressions;
using Fetchit.Listenerd.Data;
using Fetchit.Listenerd.Models;
using Fetchit.Listenerd.Service;
using Microsoft.EntityFrameworkCore;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

public class MQTTClient
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MQTTClient> _logger;
    private MqttClientOptions? _mqttOptions;
    private IMqttClient? _mqttClient;
    private MqttConfiguration? _mqttConfiguration;
    private ConnectionSecretDto? _connectionSecret;
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

            var decodedSecret = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(_mqttConfiguration.ConnectionSecret));
            _connectionSecret = System.Text.Json.JsonSerializer.Deserialize<ConnectionSecretDto>(decodedSecret)
                ?? throw new InvalidOperationException("Invalid connection secret.");
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Error configuring MQTT client");
            throw;
        }
    }

    public async Task InitializeMqttBroker()
    {
        try
        {
            if (_connectionSecret == null || _mqttConfiguration == null)
            {
                _logger.LogError("Cannot initialize MQTT broker - configuration is null. Call LoadMqttSettingsAsync first.");
                return;
            }

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
                    .WithWebSocketServer(options =>
                    {
                        options.WithUri(_connectionSecret.Broker);
                    })
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

            _mqttClient.ConnectedAsync += e =>
            {
                _connected = true;
                _logger.LogInformation("MQTT Connected (v3.1.1).");
                return Task.CompletedTask;
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

            // Actually connect to the MQTT broker
            _logger.LogInformation("Connecting to MQTT broker...");
            await _mqttClient.ConnectAsync(_mqttOptions);
            _logger.LogInformation("MQTT connection initiated");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing MQTT client");
        }
    }

    private bool EnsureConnected()
    {
        if (_mqttClient != null && _mqttClient.IsConnected)
        {
            return true;
        }

        if (!_connected)
        {
            _logger.LogWarning("MQTT client not connected");
        }

        return false;
    }

    public async Task PublishSipAsync(Fetchit.Listenerd.SipPacket packet)
    {
        if (!EnsureConnected())
        {
            _logger.LogWarning("Cannot publish SIP message - MQTT client not connected");
            return;
        }

        if (_mqttConfiguration == null || _mqttClient == null)
        {
            _logger.LogWarning("Cannot publish SIP message - MQTT configuration is null");
            return;
        }

        // Use the rich properties parsed by the class
        var payload = new
        {
            _startTime = DateTime.UtcNow.ToString("o"),
            _endTime = DateTime.UtcNow.ToString("o"),
            Guid = Guid.NewGuid().ToString(),
            ClientID = _connectionSecret?.ClientId ?? "UnknownClient",
            Number = packet.Number,
            CallerID = packet.CallerName,
            Line = "Main",
            PhoneSystem = "Fetchit Listenerd"
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
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .WithRetainFlag(false)
                .Build();

            _logger.LogInformation("Publishing SIP message to MQTT topic {Topic}", _mqttConfiguration.TopicPublish);
            _logger.LogInformation("MQTT Payload: {Payload}", jsonPayload);

            await _mqttClient.PublishAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish SIP message to MQTT");
        }
    }

}