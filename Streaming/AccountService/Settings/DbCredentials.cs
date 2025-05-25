namespace AccountService.Settings;

public class DbCredentials
{
    public string User { get; set; }
    public string Password { get; set; }
    public string Db { get; set; }
    public string Host { get; set; }
    public string Port { get; set; }

    public string ToConnectionString()
    {
        return $"Host={Host};Port={Port};Database={Db};Username={User};Password={Password};";
    }
}