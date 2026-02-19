using Fetchit.Listenerd.Options;
using Fetchit.Listenerd.Service;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharpPcap;
using SharpPcap.LibPcap;
using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Fetchit.Listenerd
{
    public class SipPacket
    {
        // Static fields for performance optimization
        private static readonly char[] LineBreakChars = new[] { '\r', '\n' };
        private static readonly Regex ServerHeaderRegex = new Regex(@"^Server\s*:",
            RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public string SourceIp { get; }
        public string DestinationIp { get; }
        public string RawSipText { get; }
        public int TotalPacketsReceived { get; }

        // Parsed Properties
        public string FromRaw { get; }
        public string CSeqRaw { get; }
        public string Number { get; }
        public string CallerName { get; }
        public bool IsInvite { get; }

        public SipPacket(
            string sourceIp,
            string destinationIp,
            string sipText,
            int totalPacketsReceived)
        {
            SourceIp = sourceIp;
            DestinationIp = destinationIp;
            RawSipText = sipText;
            TotalPacketsReceived = totalPacketsReceived;

            // Perform parsing during initialization
            FromRaw = GetSipHeaderRaw(sipText, "From");
            CSeqRaw = GetSipHeaderRaw(sipText, "CSeq");
            Number = GetSipNumber(sipText, "From");
            CallerName = GetSipDisplayName(sipText, "From");

            // Determine if it is specifically an INCOMING call invite
            IsInvite = EvaluateIsIncomingInvite();
        }

        private string GetSipNumber(string sipData, string header)
        {
            var match = Regex.Match(
                sipData,
                $@"^{Regex.Escape(header)}:\s*.*?<sip:([^@>]+)",
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

        private bool EvaluateIsIncomingInvite()
        {
            if (string.IsNullOrWhiteSpace(RawSipText)) return false;

            // 1. Basic Check: Is this a SIP Request (not a response) and is the method INVITE?
            bool isInviteRequest = RawSipText.StartsWith("INVITE", StringComparison.OrdinalIgnoreCase) &&
                                   CSeqRaw.Contains("INVITE", StringComparison.OrdinalIgnoreCase);

            if (!isInviteRequest)
            {
                // Only log if it's a common method like REGISTER to avoid spamming for every single packet
                if (RawSipText.StartsWith("REGISTER") || RawSipText.StartsWith("OPTIONS"))
                {
                    Console.WriteLine($"[DEBUG] Filtered: Not an INVITE (Method: {RawSipText.Split(' ')[0]})");
                }
                return false;
            }

            // 2. Direction Check: Is the Request-URI (the first line) targeting our Destination IP?
            int firstLineEnd = RawSipText.IndexOfAny(LineBreakChars);
            string firstLine = firstLineEnd >= 0 ? RawSipText.Substring(0, firstLineEnd) : RawSipText;
            bool isTargetingLocalIp = firstLine.Contains(DestinationIp);

            if (!isTargetingLocalIp)
            {
                Console.WriteLine($"[DEBUG] Filtered: INVITE not targeting local IP. Target: '{firstLine}' vs Local: '{DestinationIp}'");
                return false;
            }

            // 3. Source Identity: PBX vs Phone
            bool hasServerHeader = ServerHeaderRegex.IsMatch(RawSipText);

            if (!hasServerHeader)
            {
                Console.WriteLine("[DEBUG] Filtered: INVITE has no 'Server:' header (likely from a phone, not a PBX).");
                return false;
            }

            Console.WriteLine($"[DEBUG] ACCEPTED: Incoming INVITE from {SourceIp} to {DestinationIp}");
            return true;
        }
    }

    public class PacketCaptureService
    {
        private ICaptureDevice? _device;
        private readonly PacketCaptureSettings _settings;
        private readonly ILogger<PacketCaptureService> _logger;
        private MQTTClient _mqtt = null!;

        private int _totalPacketsReceived;
        private volatile bool _stopping;

        private readonly Channel<SipPacket> _sipQueue =
            Channel.CreateBounded<SipPacket>(new BoundedChannelOptions(5000)
            {
                SingleReader = true,
                FullMode = BoundedChannelFullMode.DropOldest
            });

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
                    // The 'sip' object here is an instance of the SipPacket class
                    // which already contains the parsed CallerName, Number, etc.
                    await _mqtt.PublishSipAsync(sip);
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
