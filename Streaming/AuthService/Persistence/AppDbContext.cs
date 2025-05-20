using AuthService.Models;
using AuthService.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AuthService.Persistence;

public class AppDbContext(
    IOptions<DbCredentials> dbCredentials,
    IOptions<EncryptionSettings> encryptionOptions,
    IOptions<AuthSettings> authOptions
) : DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<UserRegRequest> Requests { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured) optionsBuilder.UseNpgsql(dbCredentials.Value.ToConnectionString());
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(u => u.Email)
                .HasMaxLength(authOptions.Value.EmailSize)
                .IsRequired();

            entity.Property(u => u.PasswordHash)
                .HasMaxLength(encryptionOptions.Value.BdHashSize)
                .IsRequired();
        });

        modelBuilder.Entity<UserRegRequest>(entity =>
        {
            entity.Property(u => u.Email)
                .HasMaxLength(authOptions.Value.EmailSize)
                .IsRequired();

            entity.Property(u => u.PasswordHash)
                .HasMaxLength(encryptionOptions.Value.BdHashSize)
                .IsRequired();

            entity.Property(u => u.RegistrationCode)
                .HasMaxLength(authOptions.Value.RegistrationCodeSize);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.Property(u => u.Token)
                .HasMaxLength(encryptionOptions.Value.RefreshTokenSize)
                .IsRequired();
        });
    }
}