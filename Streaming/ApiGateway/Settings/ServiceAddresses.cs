namespace ApiGateway.Settings;

public class ServiceAddresses
{
    public ServiceConfig AuthService { get; set; } = new();
}

public class ServiceConfig
{
    public string Host { get; set; }
    public string HttpPort { get; set; }
    public string GrpcPort { get; set; }

    public string GetHttpUrl() => BuildUri("http", HttpPort);
    public string GetGrpcUrl() => BuildUri("http", GrpcPort);

    private string BuildUri(string scheme, string port)
    {
        var builder = new UriBuilder
        {
            Scheme = scheme,
            Host = Host.Trim()
        };

        if (!string.IsNullOrWhiteSpace(port) && int.TryParse(port, out var parsedPort))
            builder.Port = parsedPort;

        return builder.ToString();
    }
}