using Fetchit.Listenerd.Service;
using PacketDotNet;
using SharpPcap;
using System.Net.Sockets;
using System.Text;

namespace Fetchit.Listenerd;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly PacketCaptureService _captureService;
    private readonly MQTTClient _mqtt;
    public Worker(ILogger<Worker> logger, PacketCaptureService captureService, MQTTClient mqtt)
    {
        _logger = logger;
        _captureService = captureService;
        _mqtt = mqtt;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Fetchit Listenerd Worker starting...");
        
        // Wait for MQTT configuration to be available
        bool configLoaded = false;
        while (!configLoaded && !stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _mqtt.LoadMqttSettingsAsync();
                configLoaded = true;
                _logger.LogInformation("MQTT configuration loaded successfully");
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("Waiting for MQTT configuration to be set up via web interface...");
                _logger.LogDebug(ex, "Configuration not found");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading MQTT configuration, retrying in 30 seconds...");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        if (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Worker cancelled before configuration was loaded");
            return;
        }

        // Initialize MQTT and packet capture
        try
        {
            await _mqtt.InitializeMqttBroker();
            _captureService.AssignMqttClient(_mqtt);
            _captureService.StartSipWorker();
            _captureService.InitializeDevice();
            _captureService.ConfigureDevice();
            
            _logger.LogInformation("Worker started successfully - monitoring SIP traffic");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize services");
            throw;
        }

        // Main loop - heartbeat
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Worker heartbeat at: {time}", DateTimeOffset.Now);
            }
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }

        _logger.LogInformation("Worker stopping");
        _captureService.Stop();
    }

}
