using Fetchit.Listenerd.Options;
using Fetchit.Listenerd.Service;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PacketDotNet;
using SharpPcap;
using SharpPcap.LibPcap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fetchit.Listenerd
{
    public class PacketCaptureService
    {
        private ICaptureDevice? _device;
        private readonly PacketCaptureSettings _settings;
        private readonly ILogger<PacketCaptureService> _logger;
        private MQTTClient.MqttService _mqtt;
        private int _totalPacketsReceived = 0;
        private int _sipPacketsProcessed = 0;
        private DateTime _lastPacketTime = DateTime.MinValue;

        public PacketCaptureService(IOptions<PacketCaptureSettings> settings, ILogger<PacketCaptureService> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        public void Start(MQTTClient.MqttService mqtt)
        {
            _mqtt = mqtt;
            InitializeDevice();
            ConfigureDevice();
            _device!.StartCapture();
            _logger.LogInformation("✓ Packet capture started successfully");
            
            // Start a background task to log statistics
            //Task.Run(async () =>
            //{
            //    while (_device != null)
            //    {
            //        await Task.Delay(30000); // Every 30 seconds
            //        var timeSinceLastPacket = _lastPacketTime == DateTime.MinValue 
            //            ? "Never" 
            //            : $"{(DateTime.Now - _lastPacketTime).TotalSeconds:F0}s ago";
            //        _logger.LogInformation(
            //            "📊 Stats: Total packets: {TotalPackets}, SIP packets: {SipPackets}, Last packet: {LastPacket}",
            //            _totalPacketsReceived, _sipPacketsProcessed, timeSinceLastPacket);
            //    }
            //});
        }

        public void Stop()
        {
            if (_device == null) return;

            _device.StopCapture();
            _device.Close();
            //_logger.LogInformation("Packet capture stopped. Total: {Total}, SIP: {Sip}", 
            //    _totalPacketsReceived, _sipPacketsProcessed);
        }

        private void InitializeDevice()
        {
            var devices = CaptureDeviceList.Instance;
            if (devices.Count < 1)
                throw new Exception("No network devices found.");

            // Log available devices
            _logger.LogInformation("Available network devices ({Count}):", devices.Count);
            for (int i = 0; i < devices.Count; i++)
            {
                var device = devices[i];
                _logger.LogInformation("  [{Index}] {Name}", i, device.Name);
                _logger.LogInformation("      Description: {Description}", device.Description);
            }
        
            _device = _settings.DeviceSelectionMode.ToLower() switch
            {
                "loopback" => devices.FirstOrDefault(d =>
                    d.Name.Contains("loopback", StringComparison.OrdinalIgnoreCase) ||
                    d.Description.Contains("loopback", StringComparison.OrdinalIgnoreCase)),
                "first" => devices.FirstOrDefault() ?? devices[0],
                "byname" when !string.IsNullOrEmpty(_settings.DeviceName) =>
                    devices.FirstOrDefault(d =>
                        d.Name.Contains(_settings.DeviceName, StringComparison.OrdinalIgnoreCase) ||
                        d.Description.Contains(_settings.DeviceName, StringComparison.OrdinalIgnoreCase)),
                "auto" => devices.FirstOrDefault(d =>
                    d.Name.Contains("loopback", StringComparison.OrdinalIgnoreCase) ||
                    d.Description.Contains("loopback", StringComparison.OrdinalIgnoreCase))
                    ?? devices.FirstOrDefault()
                    ?? devices[0],
                "any" => devices.FirstOrDefault(d => 
                    d.Name.Equals("any", StringComparison.OrdinalIgnoreCase))
                    ?? devices.FirstOrDefault()
                    ?? devices[0],
                _ => devices[0]
            };

            if (_device == null)
                throw new Exception($"No suitable device found. Mode: {_settings.DeviceSelectionMode}, Name: {_settings.DeviceName}");

            _logger.LogInformation("✓ Selected device: {Name}", _device.Name);
            _logger.LogInformation("  Description: {Description}", _device.Description);
        }

        private void ConfigureDevice()
        {
            _device!.OnPacketArrival += OnPacketArrival;
            
            try
            {
                // Open in promiscuous mode
                _device.Open(DeviceModes.Promiscuous, 1000);
                _logger.LogInformation("✓ Device opened in promiscuous mode");
                
                _device.Filter = $"udp port {_settings.SipPort}";
                _logger.LogInformation("✓ Device configured with filter: udp port {Port}", _settings.SipPort);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to configure device");
                throw;
            }
        }

        private void OnPacketArrival(object sender, PacketCapture e)
        {
            _totalPacketsReceived++;
            _lastPacketTime = DateTime.Now;
            
            _logger.LogInformation("🔵 Packet received! Total: {Total}", _totalPacketsReceived);
            
            try
            {
                var raw = e.GetPacket();
                _logger.LogInformation("Raw packet length: {Length}", raw.Data.Length);
                
                var packet = Packet.ParsePacket(raw.LinkLayerType, raw.Data);

                var ip = packet.Extract<IPv4Packet>();
                var udp = packet.Extract<UdpPacket>();

                if (ip == null || udp == null)
                {
                    _logger.LogWarning("Packet received but not IPv4/UDP - LinkLayer: {LinkLayer}", raw.LinkLayerType);
                    return;
                }

                _logger.LogDebug("UDP packet: {SourceIp}:{SourcePort} -> {DestIp}:{DestPort}",
                    ip.SourceAddress, udp.SourcePort, ip.DestinationAddress, udp.DestinationPort);

                // Only SIP packets
                if (udp.SourcePort != _settings.SipPort && udp.DestinationPort != _settings.SipPort)
                {
                    _logger.LogDebug("UDP packet but not on SIP port");
                    return;
                }

                var payload = udp.PayloadData;
                if (payload == null)
                {
                    _logger.LogDebug("SIP packet with no payload");
                    return;
                }
                
                string sourceIp = ip.SourceAddress.ToString();
                string destinationIp = ip.DestinationAddress.ToString();
                string sipText = Encoding.UTF8.GetString(payload);

                _sipPacketsProcessed++;
                _logger.LogInformation("📞 SIP Packet #{Count}: {SourceIp} -> {DestIp}", 
                    _sipPacketsProcessed, sourceIp, destinationIp);

                _logger.LogDebug("SIP Content: {Content}", 
                    sipText.Length > 200 ? sipText.Substring(0, 200) + "..." : sipText);

                _mqtt.PublishSipAsync(sourceIp, destinationIp, sipText);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing packet");
            }
        }
    }
}
