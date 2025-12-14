using System.ComponentModel.DataAnnotations;

namespace Fetchit.WebPage.Models;

public class MqttSettingsViewModel
{
    [Required(ErrorMessage = "Connection Secret is required")]
    [Display(Name = "Connection Secret")]
    public string ConnectionSecret { get; set; } = string.Empty;

    [Required(ErrorMessage = "Broker Port is required")]
    [Range(1, 65535, ErrorMessage = "Port must be between 1 and 65535")]
    [Display(Name = "Broker Port")]
    public int BrokerPort { get; set; } = 443;

    [Required(ErrorMessage = "Topic Subscribe is required")]
    [Display(Name = "Topic Subscribe")]
    public string TopicSubscribe { get; set; } = string.Empty;

    [Required(ErrorMessage = "Topic Publish is required")]
    [Display(Name = "Topic Publish")]
    public string TopicPublish { get; set; } = string.Empty;
}
