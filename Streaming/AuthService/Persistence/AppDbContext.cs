using AuthService.Domain.Models;
using AuthService.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AuthService.Persistence;


public class AppDbContext(
    IOptions<DbConnectionSettings> dbConnectionOptions,
    IOptions<EncryptionSettings> encryptionOptions,
    IOptions<AuthSettings> authOptions 
    ) : DbContext
{
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<UserRegRequest> Requests { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseNpgsql(dbConnectionOptions.Value.DefaultConnection);
        }
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
                
            entity.Property(u => u.PasswordSalt)
                .HasMaxLength(encryptionOptions.Value.BdSaltSize)
                .IsRequired();

            entity.Property(u => u.RefreshToken)
                .HasMaxLength(encryptionOptions.Value.RefreshTokenSize);
        });
        
        modelBuilder.Entity<UserRegRequest>(entity =>
        {
            entity.Property(u => u.Email)
                .HasMaxLength(authOptions.Value.EmailSize)
                .IsRequired();

            entity.Property(u => u.PasswordHash)
                .HasMaxLength(encryptionOptions.Value.BdHashSize)
                .IsRequired();
                
            entity.Property(u => u.PasswordSalt)
                .HasMaxLength(encryptionOptions.Value.BdSaltSize)
                .IsRequired();

            entity.Property(u => u.RegistrationCode)
                .HasMaxLength(authOptions.Value.RegistrationCodeSize);
        });
    }
}