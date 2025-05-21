# CloudinaryUtils

A reusable .NET library for integrating [Cloudinary](https://cloudinary.com/) with your ASP.NET Core applications.  
Provides easy dependency injection setup and a ready-to-use health check for Cloudinary availability.

---

## Features

- **Strongly-typed configuration** via `CloudinarySettings`
- **Extension methods** for registering Cloudinary as a singleton
- **Cloudinary health check** for ASP.NET Core Health Checks, with customizable name and tags

---

## Installation

You can add the package via NuGet:

```sh
dotnet add package CloudinaryUtils
```

---

## Usage

### 1. Configure your `appsettings.json`:

```json
{
  "Cloudinary": {
    "CloudName": "your-cloud-name",
    "ApiKey": "your-api-key",
    "ApiSecret": "your-api-secret"
  }
}
```

### 2. Register Cloudinary and HealthCheck in your `Program.cs` or `Startup.cs`:

```csharp
using CloudinaryUtils.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Register Cloudinary (reads "Cloudinary" section by default)
builder.Services.AddCloudinaryFromConfig(builder.Configuration);

// Add HealthChecks and Cloudinary health check (customize name/tags as needed)
builder.Services.AddHealthChecks()
    .AddCloudinaryHealthCheck(name: "cloudinary", tags: new[] { "external", "ready" });
```

---

## API Reference

### CloudinarySettings

```csharp
public class CloudinarySettings
{
    public required string CloudName { get; set; }
    public required string ApiKey { get; set; }
    public required string ApiSecret { get; set; }
}
```

### Extension Methods

- **AddCloudinaryFromConfig**
    - Registers Cloudinary as a singleton using configuration section (default: `"Cloudinary"`).
    - Signature:
      ```csharp
      IServiceCollection AddCloudinaryFromConfig(
          this IServiceCollection services,
          IConfiguration configuration,
          string sectionName = "Cloudinary")
      ```

- **AddCloudinaryHealthCheck**
    - Adds a Cloudinary health check with optional name and tags.
    - Signature:
      ```csharp
      IHealthChecksBuilder AddCloudinaryHealthCheck(
          this IHealthChecksBuilder builder,
          string name = "cloudinary",
          params string[] tags)
      ```

---

## Health Check

The health check will attempt to contact Cloudinary using your credentials and report the status through ASP.NET Core Health Checks infrastructure.

---

## License

MIT

---