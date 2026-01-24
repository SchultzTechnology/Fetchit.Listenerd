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
    private readonly MQTTClient.MqttService _mqtt;
    public Worker(ILogger<Worker> logger, PacketCaptureService captureService, MQTTClient.MqttService mqtt)
    {
        _logger = logger;
        _captureService = captureService;
        _mqtt = mqtt;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _mqtt.InitializeAsync();
        _captureService.Start(_mqtt);

        _logger.LogInformation("Worker started successfully");

        while (!stoppingToken.IsCancellationRequested)
        {
            // Heartbeat every 5 minutes instead of every second
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
