﻿namespace ApiGateway.Settings;

public class ServiceAddresses
{
    public ServiceConfig AuthService { get; set; } = new();
}

public class ServiceConfig
{
    public string Host { get; set; }
    public string Port { get; set; }

    public string GetHttpUrl() => BuildUri("http", Port);
    public string GetGrpcUrl() => BuildUri("http", Port);

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