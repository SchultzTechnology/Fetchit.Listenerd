using Fetchit.Listenerd.Options;
using Fetchit.Listenerd.Service;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharpPcap;
using SharpPcap.LibPcap;
using System;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Fetchit.Listenerd
{
    public class PacketCaptureService
    {
        private ICaptureDevice? _device;
        private readonly PacketCaptureSettings _settings;
        private readonly ILogger<PacketCaptureService> _logger;
        private MQTTClient _mqtt;

        private int _totalPacketsReceived;
        private volatile bool _stopping;

        private readonly Channel<SipPacket> _sipQueue =
            Channel.CreateBounded<SipPacket>(new BoundedChannelOptions(5000)
            {
                SingleReader = true,
                FullMode = BoundedChannelFullMode.DropOldest
            });

        private record SipPacket(
            string SourceIp,
            string DestinationIp,
            string SipText,
            int TotalPacketsReceived);

        public PacketCaptureService(
            IOptions<PacketCaptureSettings> settings,
            ILogger<PacketCaptureService> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        internal void AssignMqttClient(MQTTClient mqtt)
        {
            _mqtt = mqtt;
        }

        internal void Stop()
        {
            _stopping = true;

            if (_device == null)
                return;

            try
            {
                _device.OnPacketArrival -= OnPacketArrival;
                _device.OnCaptureStopped -= OnCaptureStopped;

                if (_device.Started)
                    _device.StopCapture();

                _device.Close();
                _device.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during device shutdown");
            }
            finally
            {
                _device = null;
            }
        }

        internal void StartSipWorker()
        {
            Task.Run(async () =>
            {
                await foreach (var sip in _sipQueue.Reader.ReadAllAsync())
                {
                    _mqtt.BuildSipMessage(
                        sip.SourceIp,
                        sip.DestinationIp,
                        sip.SipText,
                        sip.TotalPacketsReceived);

                    await _mqtt.Publ
                }
            });
        }

        internal void InitializeDevice()
        {
            var devices = CaptureDeviceList.Instance;

            if (devices.Count < 1)
                throw new Exception("No network devices found.");

            for (int i = 0; i < devices.Count; i++)
            {
                _logger.LogInformation(
                    "  [{Index}] {Name} - {Description}",
                    i,
                    devices[i].Name,
                    devices[i].Description);
            }

            _device = _settings.DeviceSelectionMode.ToLower() switch
            {
                "loopback" => devices.FirstOrDefault(d =>
                    d.Name.Contains("loopback", StringComparison.OrdinalIgnoreCase) ||
                    d.Description.Contains("loopback", StringComparison.OrdinalIgnoreCase)),

                "first" => devices.FirstOrDefault(),

                "byname" when !string.IsNullOrEmpty(_settings.DeviceName) =>
                    devices.FirstOrDefault(d =>
                        d.Name.Contains(_settings.DeviceName, StringComparison.OrdinalIgnoreCase) ||
                        d.Description.Contains(_settings.DeviceName, StringComparison.OrdinalIgnoreCase)),

                "auto" => devices.FirstOrDefault(d =>
                    d.Name.Contains("loopback", StringComparison.OrdinalIgnoreCase))
                    ?? devices.FirstOrDefault(),

                "any" => devices.FirstOrDefault(d =>
                    d.Name.Equals("any", StringComparison.OrdinalIgnoreCase))
                    ?? devices.FirstOrDefault(),

                _ => devices.FirstOrDefault()
            };

            if (_device == null)
                throw new Exception("No suitable capture device found.");

            _logger.LogInformation("Selected device: {Name}", _device.Name);
            _logger.LogInformation("Description: {Description}", _device.Description);
        }

        internal void ConfigureDevice()
        {
            try
            {
                _device!.Open(DeviceModes.Promiscuous, 1000);

                _device!.Filter = $"udp port {_settings.SipPort}";
                _device.OnPacketArrival += OnPacketArrival;
                _device.OnCaptureStopped += OnCaptureStopped;

                _device.StartCapture();

                _logger.LogInformation(
                    "Device configured. Filter: udp port {Port}",
                    _settings.SipPort);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to configure device");
                throw;
            }
        }

        private void OnCaptureStopped(object sender, CaptureStoppedEventStatus status)
        {
            _logger.LogError(
                "🛑 Capture stopped. Reason={Status}",
                status);
        }

        private void OnPacketArrival(object sender, PacketCapture e)
        {
            if (_stopping)
                return;

            _totalPacketsReceived++;

            if (_device == null || !_device.Started)
                return;

            // SAFE: directly use packet bytes (no native calls)
            ReadOnlySpan<byte> data = e.Data;
            if (data.IsEmpty)
                return;

            int length = data.Length;

            // Optional but recommended: link type safety
            if (e.Device.LinkType != _device.LinkType)
                return;

            // -------- Ethernet --------
            if (length < 14)
                return;

            int etherType = (data[12] << 8) | data[13];

            // Only IPv4
            if (etherType != 0x0800)
                return;

            // -------- IPv4 --------
            int ipStart = 14;

            byte versionAndHeaderLen = data[ipStart];
            int ipVersion = versionAndHeaderLen >> 4;
            if (ipVersion != 4)
                return;

            int ipHeaderLen = (versionAndHeaderLen & 0x0F) * 4;
            if (length < ipStart + ipHeaderLen)
                return;

            // Protocol (UDP = 17)
            if (data[ipStart + 9] != 17)
                return;

            string srcIp =
                $"{data[ipStart + 12]}.{data[ipStart + 13]}.{data[ipStart + 14]}.{data[ipStart + 15]}";

            string dstIp =
                $"{data[ipStart + 16]}.{data[ipStart + 17]}.{data[ipStart + 18]}.{data[ipStart + 19]}";

            // -------- UDP --------
            int udpStart = ipStart + ipHeaderLen;
            if (length < udpStart + 8)
                return;

            int srcPort = (data[udpStart] << 8) | data[udpStart + 1];
            int dstPort = (data[udpStart + 2] << 8) | data[udpStart + 3];

            if (srcPort != _settings.SipPort && dstPort != _settings.SipPort)
                return;

            int udpLen = (data[udpStart + 4] << 8) | data[udpStart + 5];
            if (udpLen < 8 || length < udpStart + udpLen)
                return;

            // -------- SIP Payload --------
            int payloadStart = udpStart + 8;
            int payloadLen = udpLen - 8;

            if (payloadLen <= 0)
                return;

            string sipText;

            try
            {
                sipText = Encoding.ASCII.GetString(
                    data.Slice(payloadStart, payloadLen));
            }
            catch (Exception ex)
            {
                _logger.LogError($"{ex} Failed to extract SIP text.");
                return;
            }
            // _logger.LogInformation($"{_totalPacketsReceived}. packet capture");
            _sipQueue.Writer.TryWrite(
                new SipPacket(
                    srcIp,
                    dstIp,
                    sipText,
                    _totalPacketsReceived));
        }

    }
}
