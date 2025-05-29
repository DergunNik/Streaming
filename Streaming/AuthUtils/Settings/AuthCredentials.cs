namespace AuthUtils.Settings;

/// <summary>
/// Configuration model for authentication credentials
/// </summary>
public class AuthCredentials
{
    /// <summary>
    /// Service account email
    /// </summary>
    public required string ServiceEmail { get; set; }

    /// <summary>
    /// Service account password
    /// </summary>
    public required string ServicePassword { get; set; }
}