namespace AuthService.Service.HelpersInterfaces;

public interface IHashService
{
    Task<string> HashAsync(string password, string salt);
    Task<bool> CheckPasswordAsync(string password, string hashSalt);
}