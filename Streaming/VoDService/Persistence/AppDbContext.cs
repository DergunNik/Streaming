using AuthService.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VoDService.Models;
using VoDService.Settings;

namespace VoDService.Persistence
{
    public class AppDbContext : DbContext
    {
        private readonly string _connectionString;
        private readonly CloudinarySettings _cloudinarySettings;

        public AppDbContext(
            DbContextOptions<AppDbContext> options, 
            IOptions<CloudinarySettings> cloudinarySettings,
            IOptions<DbCredentials> dbCredentials)
            : base(options)
        {
            _connectionString = dbCredentials.Value.ToConnectionString();
            _cloudinarySettings = cloudinarySettings.Value;
        }

        public DbSet<Reaction> Reactions { get; set; }
        public DbSet<VideoInfo> Videos { get; set; }

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

            modelBuilder.Entity<Reaction>(entity =>
            {
                entity.HasKey(e => new { e.PublicId, e.UserId });
                entity.Property(e => e.IsLike).IsRequired();

                entity
                    .HasOne<VideoInfo>()
                    .WithMany(v => v.Reactions)
                    .HasForeignKey(e => e.PublicId)
                    .HasPrincipalKey(v => v.PublicId);
            });

            modelBuilder.Entity<VideoInfo>(entity =>
            {
                entity.HasKey(v => v.PublicId);

                entity.Property(v => v.PublicId)
                      .HasMaxLength(_cloudinarySettings.PublicIdMaxSize)
                      .IsRequired();
            });
        }
    }
}
