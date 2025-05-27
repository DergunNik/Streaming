using Microsoft.EntityFrameworkCore.Design;
using AccountService.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AccountService.Data;

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

        var cloudinarySettings = new CloudinaryRestrictions();
        configuration.GetSection("CloudinaryRestrictions").Bind(cloudinarySettings); 
        var optionsCloudinarySettings = Options.Create(cloudinarySettings);

        var contentRestrictions = new ContentRestrictions();
        configuration.GetSection("ContentRestrictions").Bind(contentRestrictions); 
        var optionsContentRestrictions = Options.Create(contentRestrictions);

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new AppDbContext(
            optionsBuilder.Options,
            optionsCloudinarySettings,
            optionsContentRestrictions,
            optionsDbCredentials
        );
    }
}