namespace AuthService.Models;

public class RefreshToken : Entity
{
    public string Token { get; set; }
    public DateTime ExpiresOnUtc { get; set; }

    public int UserId { get; set; }
    public User User { get; set; }
}