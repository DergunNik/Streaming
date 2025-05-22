using LiveService.Models;
using LiveService.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
    
namespace LiveService.Data;

public class AppDbContext : DbContext
{
    private readonly string _connectionString;
    private readonly CloudinaryRestrictions _cloudinaryRestrictions;

    public AppDbContext(
        DbContextOptions<AppDbContext> options, 
        IOptions<CloudinaryRestrictions> cloudinarySettings,
        IOptions<DbCredentials> dbCredentials)
        : base(options)
    {
        _connectionString = dbCredentials.Value.ToConnectionString();
        _cloudinaryRestrictions = cloudinarySettings.Value;
    }

    public DbSet<StreamInfo> Streams { get; set; }

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

        modelBuilder.Entity<StreamInfo>(entity =>
        {
            entity.HasKey(e => e.CloudinaryStreamId);
            
            entity.Property(e => e.CloudinaryStreamId)
                .HasMaxLength(_cloudinaryRestrictions.MaxPublicIdSize);

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(_cloudinaryRestrictions.MaxNameSize);
            
            entity.Property(e => e.ArchivePublicId)
                .IsRequired()
                .HasMaxLength(_cloudinaryRestrictions.MaxPublicIdSize);

            entity.Property(e => e.AuthorId)
                .IsRequired();

            entity.HasIndex(e => e.AuthorId);
        });
    }
}
