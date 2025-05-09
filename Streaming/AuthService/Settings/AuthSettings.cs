namespace AuthService.Settings;

public class AuthSettings
{
    public TimeSpan AccessTokenLifetime { get; set; }
    public TimeSpan RefreshTokenLifetime { get; set; }
    public int EmailSize { get; set; }
    public int PasswordSize { get; set; }
    public int MinPasswordSize { get; set; }
    public int RegistrationCodeSize { get; set; }
    public int RegistrationCodeLifetimeMinutes { get; set; }
}