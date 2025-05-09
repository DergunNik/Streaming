using System.Text;
using AuthService.Service.HelpersInterfaces;
using Konscious.Security.Cryptography;

namespace AuthService.Service.HelpersImplementations;

public class Argon2HashService : IHashService
{
    public async Task<string> HashAsync(string password, string salt)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password));
        argon2.Iterations = 10;
        argon2.MemorySize = 1024;
        argon2.DegreeOfParallelism = 4;
        argon2.Salt = Convert.FromBase64String(salt);
        var hash = await argon2.GetBytesAsync(16);
        return $"{Convert.ToBase64String(hash)}-{salt}";
    }

    public async Task<bool> CheckPasswordAsync(string password, string hashAndSalt)
    {
        var parts = hashAndSalt.Split('-', 2);
        if (parts.Length != 2) return false;
        var passwordHash = parts[0];
        var passwordSalt = parts[1];
        var hash = await HashAsync(password, passwordSalt);
        var hashOnly = hash.Split('-', 2)[0];
        return hashOnly == passwordHash;
    }
}