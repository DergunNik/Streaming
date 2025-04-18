namespace AuthService.Settings;

public class EncryptionSettings
{
    public int HashSize { get; set; }
    public int SaltSize { get; set; }
    public int BdHashSize { get; set; }
    public int BdSaltSize { get; set; }
    public int RefreshTokenSize { get; set; }
}