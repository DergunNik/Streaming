using AuthService.Service.HelpersImplementations;

namespace AuthTests;

public class Argon2HashServiceTests
{
    [Fact]
    public async Task HashAsync_ShouldReturnHashAndSalt()
    {
        const string password = "TestPassword";
        var salt = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        var service = new Argon2HashService();
        
        var hash = await service.HashAsync(password, salt);

        Assert.Contains("-", hash);
        var parts = hash.Split('-', 2);
        Assert.Equal(salt, parts[1]);
    }

    [Fact]
    public async Task CheckPasswordAsync_CorrectPassword_ShouldReturnTrue()
    {
        const string password = "MySecret";
        var salt = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        var service = new Argon2HashService();
        var hashAndSalt = await service.HashAsync(password, salt);

        var result = await service.CheckPasswordAsync(password, hashAndSalt);
        
        Assert.True(result);
    }

    [Fact]
    public async Task CheckPasswordAsync_WrongPassword_ShouldReturnFalse()
    {
        const string correctPassword = "RightPass";
        const string wrongPassword = "WrongPass";
        var salt = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        var service = new Argon2HashService();
        var hashAndSalt = await service.HashAsync(correctPassword, salt);

        var result = await service.CheckPasswordAsync(wrongPassword, hashAndSalt);

        Assert.False(result);
    }
}