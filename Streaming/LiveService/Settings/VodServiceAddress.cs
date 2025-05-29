namespace LiveService.Settings;


public class VodServiceAddress
{
    public required string Host { get; set; }
    public required string Port { get; set; }
    
    
    public string Url => BuildUri("http", Port);

    private string BuildUri(string scheme, string port)
    {
        var builder = new UriBuilder { Scheme = scheme, Host = Host.Trim() };

        if (!string.IsNullOrWhiteSpace(port) && int.TryParse(port, out var parsedPort))
            builder.Port = parsedPort;

        return builder.ToString();
    }
}