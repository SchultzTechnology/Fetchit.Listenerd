namespace Fetchit.Listenerd.Settings
{
    public class MQTTSettings
    {
        public string BrokerAddress { get; set; }
        public int BrokerPort { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Topic { get; set; }
        public bool UseTls { get; set; }
    }
}