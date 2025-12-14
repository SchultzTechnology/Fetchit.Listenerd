using System.Text.Json.Serialization;

namespace Fetchit.WebPage.Models;

public class ConnectionSecretDto
{
    [JsonPropertyName("Broker")]
    public string Broker { get; set; } = string.Empty;

    [JsonPropertyName("ClientId")]
    public string ClientId { get; set; } = string.Empty;

    [JsonPropertyName("LocationId")]
    public string LocationId { get; set; } = string.Empty;

    [JsonPropertyName("UserName")]
    public string UserName { get; set; } = string.Empty;

    [JsonPropertyName("Password")]
    public string Password { get; set; } = string.Empty;
}
