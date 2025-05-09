namespace AuthService.Models;

public class UserRegRequest : Entity
{
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public required string RegistrationCode { get; set; }
    public DateTime ActiveUntil { get; set; }
}