namespace AccountService.Settings;

public class ContentRestrictions
{
    public int MaxDescriptionLength { get; set; }
    public long MaxImageSizeBytes { get; set; }
    public double AvatarAspectRatioMin { get; set; }
    public double AvatarAspectRatioMax { get; set; }
    public double BackgroundAspectRatioMin { get; set; }
    public double BackgroundAspectRatioMax { get; set; }
}