using AuthService.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VoDService.Models;
using VoDService.Settings;

namespace VoDService.Data;

public class AppDbContext : DbContext
{
    private readonly CloudinaryRestrictions _cloudinaryRestrictions;
    private readonly string _connectionString;

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        IOptions<CloudinaryRestrictions> cloudinarySettings,
        IOptions<DbCredentials> dbCredentials)
        : base(options)
    {
        _connectionString = dbCredentials.Value.ToConnectionString();
        _cloudinaryRestrictions = cloudinarySettings.Value;
    }

    public DbSet<Reaction> Reactions { get; set; }
    public DbSet<VideoInfo> Videos { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured && !string.IsNullOrEmpty(_connectionString))
            optionsBuilder.UseNpgsql(_connectionString);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Reaction>(entity =>
        {
            entity.HasKey(e => new { e.PublicId, e.UserId });
            entity.Property(e => e.IsLike).IsRequired();
        });

        modelBuilder.Entity<VideoInfo>(entity =>
        {
            entity.HasKey(v => v.PublicId);
            entity.Property(v => v.PublicId)
                .HasMaxLength(_cloudinaryRestrictions.PublicIdMaxSize);

            entity.HasMany<Reaction>()
                .WithOne()
                .HasForeignKey(r => r.PublicId)
                .HasPrincipalKey(v => v.PublicId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}