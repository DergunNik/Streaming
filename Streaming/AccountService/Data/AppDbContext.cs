using AccountService.Models;
using AccountService.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AccountService.Data;

public class AppDbContext : DbContext
{
    private readonly string _connectionString;
    private readonly CloudinaryRestrictions _cloudinarySettings;
    private readonly ContentRestrictions _restrictions;

    public AppDbContext(
        DbContextOptions<AppDbContext> options, 
        IOptions<CloudinaryRestrictions> cloudinarySettings,
        IOptions<ContentRestrictions> contentRestrictions,
        IOptions<DbCredentials> dbCredentials)
        : base(options)
    {
        _connectionString = dbCredentials.Value.ToConnectionString();
        _restrictions = contentRestrictions.Value;
        _cloudinarySettings = cloudinarySettings.Value;
    }

    public DbSet<AccountInfo> Accounts { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured && !string.IsNullOrEmpty(_connectionString))
        {
            optionsBuilder.UseNpgsql(_connectionString);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<AccountInfo>(entity =>
        {
            entity.HasKey(v => v.UserId);
            
            entity.Property(v => v.BackgroundPublicId)
                .HasMaxLength(_cloudinarySettings.PublicIdMaxSize);

            entity.Property(v => v.AvatarPublicId)
                .HasMaxLength(_cloudinarySettings.PublicIdMaxSize);

            entity.Property(v => v.Description)
                .HasMaxLength(_restrictions.MaxDescriptionLength);
        });
    }
}