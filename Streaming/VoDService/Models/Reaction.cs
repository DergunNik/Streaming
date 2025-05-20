namespace VoDService.Models;

public class Reaction
{
    public string PublicId { get; set; }
    public int UserId { get; set; }
    public bool IsLike { get; set; }
}
