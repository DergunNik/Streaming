using System.Text;
using AuthService.Domain.Interfaces;
using Konscious.Security.Cryptography;

namespace AuthService.Services;

public class Argon2HashService : IHashService
{
    public async Task<byte[]> HashAsync(string password, string salt)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password));
        argon2.Iterations = 10;
        argon2.MemorySize = 1024;
        argon2.DegreeOfParallelism = 4;
        argon2.Salt = Convert.FromBase64String(salt);
        return await argon2.GetBytesAsync(16);
    }

    public async Task<bool> CheckPasswordAsync(string password, string passwordSalt, string passwordHash)
    {
        var hash = await HashAsync(password, passwordSalt);
        return Convert.ToBase64String(hash) == passwordHash;
    }
}