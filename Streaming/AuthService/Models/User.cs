namespace AuthService.Models;

public class User : Entity
{
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public UserRole UserRole { get; set; }
    public bool IsBanned { get; set; }
}