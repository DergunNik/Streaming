namespace AuthService.Settings;

public class EmailServiceAddress
{
    public string Host { get; set; }
    public string Port { get; set; }
    
    public string GetEmailHttpUrl() => BuildUri("http", Port);
    public string GetEmailGrpcUrl() => BuildUri("http", Port);
    
    private string BuildUri(string scheme, string port)
    {
        var builder = new UriBuilder { Scheme = scheme, Host = Host.Trim() };

        if (!string.IsNullOrWhiteSpace(port) && int.TryParse(port, out var parsedPort))
            builder.Port = parsedPort;

        return builder.ToString();
    }
}