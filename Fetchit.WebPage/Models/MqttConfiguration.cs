namespace Fetchit.WebPage.Models;

public class MqttConfiguration
{
    public int Id { get; set; }
    public string ConnectionSecret { get; set; } = string.Empty;
    public int BrokerPort { get; set; }
    public string TopicSubscribe { get; set; } = string.Empty;
    public string TopicPublish { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
