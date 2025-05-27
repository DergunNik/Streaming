using AuthService.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Options;
using VoDService.Settings;

namespace VoDService.Data;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", true)
            .AddJsonFile("appsettings.Development.json", true)
            .AddEnvironmentVariables()
            .Build();

        var dbCredentials = new DbCredentials();
        configuration.GetSection("DbCredentials").Bind(dbCredentials);

        var connectionString = dbCredentials.ToConnectionString();
        var optionsDbCredentials = Options.Create(dbCredentials);

        var cloudinaryRestrictions = new CloudinaryRestrictions();
        configuration.GetSection("CloudinaryRestrictions").Bind(cloudinaryRestrictions); 
        var optionsCloudinaryRestrictions = Options.Create(cloudinaryRestrictions);

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new AppDbContext(
            optionsBuilder.Options,
            optionsCloudinaryRestrictions,
            optionsDbCredentials
        );
    }
}