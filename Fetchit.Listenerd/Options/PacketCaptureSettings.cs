namespace Fetchit.Listenerd.Options
{
    public class PacketCaptureSettings
    {
        public string DeviceSelectionMode { get; set; } = "Auto";
        
        public string? DeviceName { get; set; }
        
        public int SipPort { get; set; } = 5060;
        
    }
}
