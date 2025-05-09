namespace AuthService.Models;

public class RefreshToken
{
    public string Token { get; set; }
    public DateTime ExpiresOnUtc { get; set; }

    public int UserId { get; set; }
    public User User { get; set; }
}