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
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            }
            await Task.Delay(1000, stoppingToken);
        }
        _captureService.Stop();
    }

}
