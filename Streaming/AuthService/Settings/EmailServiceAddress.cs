namespace AuthService.Settings;

public class EmailServiceAddress
{
    public string Host { get; set; }
    public string HttpPort { get; set; }
    public string GrpcPort { get; set; }
    
    public string GetEmailHttpUrl() => BuildUri("http", HttpPort);
    public string GetEmailGrpcUrl() => BuildUri("http", GrpcPort);
    
    private string BuildUri(string scheme, string port)
    {
        var builder = new UriBuilder { Scheme = scheme, Host = Host.Trim() };

        if (!string.IsNullOrWhiteSpace(port) && int.TryParse(port, out var parsedPort))
            builder.Port = parsedPort;

        return builder.ToString();
    }
}