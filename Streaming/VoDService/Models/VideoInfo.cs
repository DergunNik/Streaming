namespace VoDService.Models;

public class VideoInfo
{
    public string PublicId { get; set; }
    public int LikeCount { get; set; }
    public int DislikeCount { get; set; }
    
    public ICollection<Reaction> Reactions { get; set; }
}
