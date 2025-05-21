namespace AccountService.Models;

public class AccountInfo
{
    public int UserId { get; set; }
    public string? AvatarPublicId { get; set; }
    public string? BackgroundPublicId { get; set; }
    public string? Description { get; set; }
    public bool IsBanned { get; set; }
}