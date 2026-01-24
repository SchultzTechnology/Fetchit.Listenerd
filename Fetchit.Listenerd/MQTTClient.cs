using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using SQLitePCL;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

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
        private string _clientId = "";

        public MqttService(ILogger<MqttService> logger, MqttConfigService configService)
        {
            _logger = logger;
            _configService = configService;
        }

        public async Task InitializeAsync()
        {
            try
            {
                var config = await _configService.GetLatestConfigurationAsync();
                if (config == null)
                {
                    _logger.LogError("No MQTT configuration found in database. Please configure MQTT settings in the web interface.");
                    return;
                }

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
                _clientId = connectionDetails.ClientId;
                string username = connectionDetails.UserName;
                string password = connectionDetails.Password;

                _logger.LogInformation("Initializing MQTT with configuration from database:");
                _logger.LogInformation("  Broker: {Broker}", broker);
                _logger.LogInformation("  Port: {Port}", port);
                _logger.LogInformation("  ClientId: {ClientId}", clientId);
                _logger.LogInformation("  Topic Publish: {TopicPublish}", _topicPublish);

                var factory = new MqttFactory();
                _mqttClient = factory.CreateMqttClient();

                bool ws = broker.StartsWith("ws");
                if (ws)
                {
                    _options = new MqttClientOptionsBuilder().WithClientId(clientId)
                        .WithWebSocketServer(broker)
                        .WithCredentials(username, password)
                        .WithCleanSession()
                        .Build();
                }
                else
                {
                    _options = new MqttClientOptionsBuilder().WithClientId(clientId)
                        .WithTcpServer(broker)
                        .WithCredentials(username, password)
                        .WithCleanSession()
                        .Build();
                }



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

        private string GetSipNumber(string sipData, string header)
        {
            var match = Regex.Match(
                sipData,
                $@"^{header}:\s*.*?<sip:([^@>]+)",
                RegexOptions.Multiline | RegexOptions.IgnoreCase);

            return match.Success ? match.Groups[1].Value : "Unknown";
        }

        private string GetSipDisplayName(string sipData, string header)
        {
            if (string.IsNullOrWhiteSpace(sipData) || string.IsNullOrWhiteSpace(header))
                return string.Empty;

            var match = Regex.Match(
                sipData,
                $@"^{Regex.Escape(header)}\s*:\s*""?([^""<]+)""?\s*<sip:",
                RegexOptions.Multiline | RegexOptions.IgnoreCase);

            return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
        }


        private string GetSipHeaderRaw(string sipData, string header)
        {
            if (string.IsNullOrWhiteSpace(sipData) || string.IsNullOrWhiteSpace(header))
                return string.Empty;

            var match = Regex.Match(
                sipData,
                $@"^{Regex.Escape(header)}\s*:\s*(.+)$",
                RegexOptions.Multiline | RegexOptions.IgnoreCase);

            if (!match.Success)
                return string.Empty;

            var value = match.Groups[1].Value.Trim();

            int uriEnd = value.IndexOf('>');
            if (uriEnd >= 0)
                return value.Substring(0, uriEnd + 1);

            return value;
        }

        public bool IsIncomingCallInvite(string callId, string cseq, string sipData)
        {
            if (string.IsNullOrEmpty(callId) || string.IsNullOrEmpty(cseq))
                return false;

            return cseq?.Contains("INVITE", StringComparison.OrdinalIgnoreCase) == true && sipData.StartsWith("INVITE", StringComparison.OrdinalIgnoreCase) == true;

        }

        public async Task PublishSipAsync(string src, string dest, string sipData, int totalPacketsReceived)
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
            string fromRaw = GetSipHeaderRaw(sipData, "From");
            string cseqRaw = GetSipHeaderRaw(sipData, "CSeq");
            string number = GetSipNumber(sipData, "From");
            string callername = GetSipDisplayName(sipData, "From");
            if (IsIncomingCallInvite(fromRaw, cseqRaw, sipData))
            {
                try
                {
                    var payload = JsonSerializer.Serialize(new
                    {
                        StartTime = DateTime.UtcNow,
                        EndTime = DateTime.UtcNow,
                        Guid = Guid.NewGuid(),
                        ClientID = _clientId,
                        Number = number,
                        CallerID = callername,
                        Line = "Main Office",
                        AtlasId = "",
                        PhoneSystem = "R-Pi"
                    });

                    _logger.LogInformation("Payload: {Payload}", payload);

                    const string prefix = "FetchitListenerdClient_";
                    if (!_clientId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        _clientId = prefix + _clientId;
                    }

                    var message = new MqttApplicationMessageBuilder()
                        .WithTopic(_topicPublish)
                        .WithPayload(payload)
                        .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                        .Build();

                    await _mqttClient.PublishAsync(message);
                    _logger.LogInformation(
                        "✅ {totalPacketsReceived}. SIP event published → CallID={CallId}, Number={Number}, Name={Name}, CSeq={CSeq}",
                        totalPacketsReceived,
                        fromRaw,
                        number,
                        callername,
                        cseqRaw);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "{totalPacketsReceived}. Failed to publish SIP event. → CallID={CallId}, Number={Number}, Name={Name}, CSeq={CSeq}",
                        totalPacketsReceived,
                        fromRaw,
                        number,
                        callername,
                        cseqRaw);
                }

            }
        }
    }
}