using System.Text.Json.Serialization;

namespace UserService.Models;

public class EventMessage
{
    [JsonPropertyName("@class")]
    public string Class { get; set; }

    [JsonPropertyName("time")]
    public long Time { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("realmId")]
    public string RealmId { get; set; }

    [JsonPropertyName("clientId")]
    public string ClientId { get; set; }

    [JsonPropertyName("userId")]
    public string UserId { get; set; }

    [JsonPropertyName("ipAddress")]
    public string IpAddress { get; set; }

    [JsonPropertyName("details")]
    public Details Details { get; set; }
}

public class Details
{
    [JsonPropertyName("context")]
    public string? Context { get; set; } 
    
    [JsonPropertyName("auth_method")]
    public string? AuthMethod { get; set; }

    [JsonPropertyName("auth_type")]
    public string? AuthType { get; set; }

    [JsonPropertyName("register_method")]
    public string? RegisterMethod { get; set; }

    [JsonPropertyName("redirect_uri")]
    public string? RedirectUri { get; set; }

    [JsonPropertyName("code_id")]
    public string? CodeId { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }
}

