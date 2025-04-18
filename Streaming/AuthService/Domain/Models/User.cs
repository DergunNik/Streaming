namespace AuthService.Domain.Models;

public class User : Entity
{
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public required string PasswordSalt { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime RefreshTokenExpiryTime { get; set; }
    public UserRole UserRole { get; set; }
    public BanStatus BanStatus { get; set; }
}
