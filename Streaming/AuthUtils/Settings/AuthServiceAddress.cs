namespace AuthUtils.Settings;

/// <summary>
/// Configuration model for AuthService address
/// </summary>
public class AuthServiceAddress
{
    /// <summary>
    /// Service host name or IP address
    /// </summary>
    public required string Host { get; set; }

    /// <summary>
    /// Service port
    /// </summary>
    public required string Port { get; set; }
    
    /// <summary>
    /// Full service URL
    /// </summary>
    public string Url => BuildUri("http", Port);

    private string BuildUri(string scheme, string port)
    {
        var builder = new UriBuilder { Scheme = scheme, Host = Host.Trim() };

        if (!string.IsNullOrWhiteSpace(port) && int.TryParse(port, out var parsedPort))
            builder.Port = parsedPort;

        return builder.ToString();
    }
}