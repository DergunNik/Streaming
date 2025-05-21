namespace CloudinaryUtils.Settings;

/// <summary>
/// Cloudinary configuration settings.
/// </summary>
public class CloudinarySettings
{
    /// <summary>
    /// The Cloudinary cloud name.
    /// </summary>
    public required string CloudName { get; set; }

    /// <summary>
    /// The Cloudinary API key.
    /// </summary>
    public required string ApiKey { get; set; }

    /// <summary>
    /// The Cloudinary API secret.
    /// </summary>
    public required string ApiSecret { get; set; }
}