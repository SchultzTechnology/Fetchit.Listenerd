using System.Text;
using System.Text.Json;
using Fetchit.WebPage.Models;

namespace Fetchit.WebPage.Services;

public class ConnectionSecretService
{
    public ConnectionSecretDto? DecodeConnectionSecret(string base64Secret)
    {
        try
        {
            var jsonBytes = Convert.FromBase64String(base64Secret);
            var jsonString = Encoding.UTF8.GetString(jsonBytes);
            return JsonSerializer.Deserialize<ConnectionSecretDto>(jsonString);
        }
        catch
        {
            return null;
        }
    }

    public string EncodeConnectionSecret(ConnectionSecretDto secret)
    {
        var jsonString = JsonSerializer.Serialize(secret);
        var jsonBytes = Encoding.UTF8.GetBytes(jsonString);
        return Convert.ToBase64String(jsonBytes);
    }
}
