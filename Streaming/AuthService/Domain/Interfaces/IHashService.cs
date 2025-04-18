namespace AuthService.Domain.Interfaces;

public interface IHashService
{
    public Task<byte[]> HashAsync(string password, string salt);
    public Task<bool> CheckPasswordAsync(string password, string passwordSalt, string passwordHash);
}