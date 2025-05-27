using AuthService.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Options;

namespace AuthService.Data;

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
        
        var encryptionSettings = new EncryptionSettings();
        configuration.GetSection("EncryptionSettings").Bind(encryptionSettings); 
        var optionsEncryptionSettings = Options.Create(encryptionSettings);

        var authSettings = new AuthSettings();
        configuration.GetSection("AuthSettings").Bind(authSettings); 
        var optionsAuthSettings = Options.Create(authSettings);

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new AppDbContext(
            optionsBuilder.Options,
            optionsDbCredentials,
            optionsEncryptionSettings,
            optionsAuthSettings
        );
    }
}