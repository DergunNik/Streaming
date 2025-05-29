# AuthUtils Library

## Overview

`AuthUtils` is a .NET library designed to simplify integration with an authentication gRPC service. It provides helper methods for dependency injection, configuration, and a client service to interact with the authentication server for operations like login, token refresh, and health checks.

## Features

*   **Dependency Injection**: Easily register and configure authentication services and health checks.
*   **Configuration Management**: Uses `IConfiguration` to load settings for service addresses and credentials.
*   **Authentication Service Client**: A client (`AuthService`) to handle JWT and refresh token retrieval and management.
*   **Health Checks**: Integrated health check for the authentication service.
*   **gRPC Client Setup**: Configures a gRPC client for the `AuthService` with round-robin load balancing.

## Setup and Configuration

### 1. Add NuGet Packages

Ensure you have the necessary NuGet packages in your project:
```xml
<PackageReference Include="Grpc.Net.Client" Version="..." />
<PackageReference Include="Microsoft.Extensions.Configuration.Binder" Version="..." />
<PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="..." />
<PackageReference Include="Microsoft.Extensions.Diagnostics.HealthChecks" Version="..." />
<PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="..." />
```

### 2. Configure Services

In your `Startup.cs` or `Program.cs` (for .NET 6+), use the extension methods to add and configure the authentication services.

#### AppSettings Example:

Your `appsettings.json` should contain sections for the authentication service address and credentials:

```json
{
  "AuthServiceAddress": {
    "Host": "localhost", // Or your auth service host
    "Port": "5001"       // Or your auth service port
  },
  "AuthCredentials": {
    "ServiceEmail": "your-service-email@example.com",
    "ServicePassword": "your-service-password"
  }
}
```

#### Dependency Injection:

```csharp
// In your service configuration (e.g., Program.cs or Startup.ConfigureServices)
using AuthUtils.DependencyInjection;

public void ConfigureServices(IServiceCollection services)
{
    // ... other service configurations

    // Add AuthUtils services
    services.AddAuthFromConfig(Configuration); // Assumes IConfiguration 'Configuration' is available

    // Add Auth health check
    services.AddHealthChecks()
            .AddAuthHealthCheck(Configuration);

    // Register IAuthService and its implementation
    services.AddScoped<AuthUtils.Services.IAuthService, AuthUtils.Services.AuthService>();

    // ... other service configurations
}
```

*   `AddCloudinaryFromConfig`: Registers and configures the gRPC client for `AuthServerApp.AuthService.AuthServiceClient`.
    *   You can optionally provide custom section names for the address and credentials if they differ from the defaults (`"AuthServiceAddress"` and `"AuthCredentials"` respectively).
*   `AddAuthHealthCheck`: Adds a health check for the authentication service.
    *   You can optionally provide a custom address section name, health check name, and tags.

### 3. Configuration Models

The library uses the following models for configuration, which are typically bound from `IConfiguration`:

*   **`AuthServiceAddress`**:
    ```csharp
    public class AuthServiceAddress
    {
        public required string Host { get; set; }
        public required string Port { get; set; }
        public string Url => $"http://{Host.Trim()}:{Port}"; // Simplified for example
    }
    ```

*   **`AuthCredentials`**:
    ```csharp
    public class AuthCredentials
    {
        public required string ServiceEmail { get; set; }
        public required string ServicePassword { get; set; }
    }
    ```

## Usage

### `IAuthService`

Inject `AuthUtils.Services.IAuthService` into your services where you need to perform authentication operations.

```csharp
using AuthUtils.Services;

public class MyService
{
    private readonly IAuthService _authService;
    private readonly ILogger<MyService> _logger;

    // Example credentials (in a real app, get these securely)
    private string _userEmail = "user@example.com";
    private string _userPassword = "password123";
    private string _currentJwtToken = null;
    private string _currentRefreshToken = null;

    public MyService(IAuthService authService, ILogger<MyService> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    public async Task EnsureAuthenticatedAndGetDataAsync()
    {
        try
        {
            // Get or refresh tokens
            (_currentJwtToken, _currentRefreshToken) = _authService.GetAccessToken(
                _currentJwtToken,
                _currentRefreshToken,
                _userEmail,
                _userPassword
            );

            _logger.LogInformation("Successfully obtained/refreshed tokens.");
            // Now use _currentJwtToken for authenticated API calls

            // Example:
            // var httpClient = new HttpClient();
            // httpClient.DefaultRequestHeaders.Authorization =
            //     new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _currentJwtToken);
            // var response = await httpClient.GetAsync("your-protected-api/data");
            // response.EnsureSuccessStatusCode();
            // var data = await response.Content.ReadAsStringAsync();
            // _logger.LogInformation("Data from protected API: {Data}", data);

        }
        catch (System.Security.Authentication.AuthenticationException authEx)
        {
            _logger.LogError(authEx, "Authentication failed.");
            // Handle authentication failure (e.g., redirect to login, show error)
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred.");
            // Handle other errors
        }
    }

    public async Task LoginAsync(string email, string password)
    {
        try
        {
            (_currentJwtToken, _currentRefreshToken) = _authService.GetAccessToken(email, password);
            _logger.LogInformation("Login successful. JWT and Refresh tokens obtained.");
        }
        catch (System.Security.Authentication.AuthenticationException authEx)
        {
            _logger.LogError(authEx, "Login failed: Invalid email or password.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login process.");
        }
    }
}
```

The `AuthService` handles:
*   Checking if the current JWT and refresh tokens are valid (JWT not expired beyond a threshold).
*   Attempting to refresh tokens if they are invalid or missing but refresh token is present.
*   Falling back to login with email and password if refresh fails or tokens are not available.

## Proto Definition (`auth.proto`)

This library communicates with a gRPC service defined by the following `.proto` file:

```protobuf
syntax = "proto3";

import "google/protobuf/empty.proto";
import "google/protobuf/timestamp.proto";

option csharp_namespace = "AuthServerApp";

package auth;

service AuthService {
  rpc BeginRegistration (RegisterRequest) returns (google.protobuf.Empty);
  rpc FinishRegistration (FinishRequest) returns (google.protobuf.Empty);
  rpc Login (LoginRequest) returns (LoginReply);
  rpc Refresh (RefreshRequest) returns (LoginReply);
  rpc Logout (google.protobuf.Empty) returns (google.protobuf.Empty);
}

message RegisterRequest {
  string email = 1;
  string password = 2;
}

message FinishRequest {
  string email = 1;
  string code = 2;
}

message LoginRequest {
  string email = 1;
  string password = 2;
}

message LoginReply {
  string jwtToken = 1;
  string refreshToken = 2;
  google.protobuf.Timestamp expiresJwt = 3;
  google.protobuf.Timestamp expiresRefresh = 4;
}

message RefreshRequest {
  string jwtToken = 1;
  string refreshToken = 2;
}
```

This definition outlines the expected gRPC service contract that `AuthUtils` interacts with. Ensure your gRPC authentication server implements this service.