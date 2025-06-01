using System.IdentityModel.Tokens.Jwt;
using System.Linq.Expressions;
using System.Security.Authentication;
using System.Security.Claims;
using System.Text;
using AuthService.Data;
using AuthService.Models;
using AuthService.Services.HelpersImplementations;
using AuthService.Services.HelpersInterfaces;
using AuthService.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;

namespace AuthTests;

public class JwtServiceTests
{
    private readonly Mock<IOptions<AuthSettings>> _authSettingsMock;
    private readonly AuthSettings _defaultAuthSettings;
    private readonly EncryptionSettings _defaultEncryptionSettings;
    private readonly Mock<IOptions<EncryptionSettings>> _encryptionSettingsMock;
    private readonly Mock<IHashService> _hashServiceMock;
    private readonly JwtSettings _jwtSettings;
    private readonly Mock<ILogger<JwtService>> _loggerMock;
    private readonly Mock<IRepository<RefreshToken>> _refreshTokenRepoMock;
    private readonly Mock<IRepository<User>> _userRepoMock;

    public JwtServiceTests()
    {
        _authSettingsMock = new Mock<IOptions<AuthSettings>>();
        _encryptionSettingsMock = new Mock<IOptions<EncryptionSettings>>();
        _loggerMock = new Mock<ILogger<JwtService>>();
        _userRepoMock = new Mock<IRepository<User>>();
        _refreshTokenRepoMock = new Mock<IRepository<RefreshToken>>();
        _hashServiceMock = new Mock<IHashService>();

        _jwtSettings = new JwtSettings
        {
            Key = "TestSuperSecretKeyForUnitTestPurposeOnly12345", // HS256 requires min 128 bits (16 bytes), this is 45 bytes.
            Issuer = "TestIssuer",
            Audience = "TestAudience"
        };

        _defaultAuthSettings = new AuthSettings
        {
            AccessTokenLifetime = TimeSpan.FromMinutes(15),
            RefreshTokenLifetime = TimeSpan.FromHours(1)
        };
        _authSettingsMock.Setup(a => a.Value).Returns(_defaultAuthSettings);

        _defaultEncryptionSettings = new EncryptionSettings
        {
            RefreshTokenSize = 32
        };
        _encryptionSettingsMock.Setup(e => e.Value).Returns(_defaultEncryptionSettings);
    }

    private JwtService CreateService()
    {
        return new JwtService(
            _authSettingsMock.Object,
            _encryptionSettingsMock.Object,
            _loggerMock.Object,
            _userRepoMock.Object,
            _refreshTokenRepoMock.Object,
            _hashServiceMock.Object,
            Options.Create(_jwtSettings)
        );
    }

    private string GenerateTestJwtTokenString(int userId, string email, UserRole role, DateTime expiryTime, string key,
        string issuer, string audience)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, email),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, role.ToString())
        };

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            expires: expiryTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Fact]
    public async Task GenerateTokenAsync_UserNotFound_ThrowsArgumentException()
    {
        _userRepoMock.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>(), default))
            .ReturnsAsync((User)null);
        var service = CreateService();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GenerateTokensAsync("user@example.com", "password"));
        Assert.Equal("Wrong email or password.", exception.Message);
    }

    [Fact]
    public async Task GenerateTokenAsync_BannedUser_ThrowsAuthenticationException()
    {
        var user = new User
        {
            Id = 1, Email = "test@example.com", PasswordHash = "hash", IsBanned = true,
            UserRole = UserRole.DefaultUser
        };
        _userRepoMock.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>(), default))
            .ReturnsAsync(user);
        var service = CreateService();

        var exception = await Assert.ThrowsAsync<AuthenticationException>(() =>
            service.GenerateTokensAsync(user.Email, "password"));
        Assert.Equal("The user is banned.", exception.Message);
    }

    [Fact]
    public async Task GenerateTokenAsync_IncorrectPassword_ThrowsArgumentException()
    {
        var user = new User
        {
            Id = 1, Email = "test@example.com", PasswordHash = "correcthash-salt", IsBanned = false,
            UserRole = UserRole.DefaultUser
        };
        _userRepoMock.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>(), default))
            .ReturnsAsync(user);
        _hashServiceMock.Setup(h => h.CheckPasswordAsync("wrongpassword", user.PasswordHash)).ReturnsAsync(false);
        var service = CreateService();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GenerateTokensAsync(user.Email, "wrongpassword"));
        Assert.Equal("Wrong email or password.", exception.Message);
    }

    [Fact]
    public async Task GenerateTokenAsync_ValidUser_ReturnsTokens()
    {
        var user = new User
        {
            Id = 1, Email = "test@example.com", PasswordHash = "correcthash-salt",
            IsBanned = false, UserRole = UserRole.DefaultUser
        };
        _userRepoMock.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<User, bool>>>(), default))
            .ReturnsAsync(user);
        _hashServiceMock.Setup(h => h.CheckPasswordAsync("password", user.PasswordHash)).ReturnsAsync(true);
        _refreshTokenRepoMock.Setup(r => r.AddAsync(It.IsAny<RefreshToken>(), default)).Returns(Task.CompletedTask);
        _refreshTokenRepoMock.Setup(r => r.SaveChangesAsync(default)).Returns(Task.CompletedTask);
        var service = CreateService();

        var (jwtToken, refreshToken) = await service.GenerateTokensAsync(user.Email, "password");

        Assert.False(string.IsNullOrEmpty(jwtToken));
        Assert.False(string.IsNullOrEmpty(refreshToken));
        _refreshTokenRepoMock.Verify(r => r.AddAsync(It.IsAny<RefreshToken>(), default), Times.Once);
        _refreshTokenRepoMock.Verify(r => r.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task RefreshTokenAsync_InvalidJwtFormat_ThrowsException()
    {
        var service = CreateService();
        var exception = await Assert.ThrowsAsync<Exception>(() =>
            service.RefreshTokenAsync("invalid-jwt-format", "valid-refresh-token"));
        Assert.Equal("Cannot read token.", exception.Message);
    }

    [Fact]
    public async Task RefreshTokenAsync_JwtMissingIdClaim_ThrowsUnauthorizedAccessException()
    {
        var service = CreateService();
        var tokenWithoutIdClaim = GenerateTestJwtTokenString(0, "test@example.com", UserRole.DefaultUser,
                DateTime.UtcNow.AddMinutes(5), _jwtSettings.Key, _jwtSettings.Issuer, _jwtSettings.Audience)
            .Replace($"\"{ClaimTypes.NameIdentifier}\":\"0\",", ""); // Crude way to remove, better to generate without

        var claims = new List<Claim>
            { new(ClaimTypes.Name, "test@example.com"), new(ClaimTypes.Role, UserRole.DefaultUser.ToString()) };
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        var tokenDescriptor = new JwtSecurityToken(
            _jwtSettings.Issuer,
            _jwtSettings.Audience,
            claims, // No NameIdentifier
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);
        var jwtWithoutId = new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);


        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.RefreshTokenAsync(jwtWithoutId, "valid-refresh-token"));
        Assert.Equal("Invalid token.", exception.Message);
    }

    [Fact]
    public async Task RefreshTokenAsync_UserNotFoundForJwtId_ThrowsUnauthorizedAccessException()
    {
        var service = CreateService();
        var jwtForNonExistentUser = GenerateTestJwtTokenString(999, "test@example.com", UserRole.DefaultUser,
            DateTime.UtcNow.AddMinutes(5), _jwtSettings.Key, _jwtSettings.Issuer, _jwtSettings.Audience);
        _userRepoMock.Setup(r => r.GetByIdAsync(999, default, null)).ReturnsAsync((User)null);

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.RefreshTokenAsync(jwtForNonExistentUser, "valid-refresh-token"));
        Assert.Equal("Invalid token.", exception.Message);
    }

    [Fact]
    public async Task RefreshTokenAsync_DbRefreshTokenNotFound_ThrowsUnauthorizedAccessException()
    {
        var user = new User
        {
            Id = 1, Email = "test@example.com", PasswordHash = "pwd", UserRole = UserRole.DefaultUser,
            IsBanned = false
        };
        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id, default, null)).ReturnsAsync(user);
        _refreshTokenRepoMock
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<RefreshToken, bool>>>(), default))
            .ReturnsAsync((RefreshToken)null);
        var service = CreateService();
        var validJwt = GenerateTestJwtTokenString(user.Id, user.Email, user.UserRole, DateTime.UtcNow.AddMinutes(5),
            _jwtSettings.Key, _jwtSettings.Issuer, _jwtSettings.Audience);

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.RefreshTokenAsync(validJwt, "non-existent-db-refresh-token"));
        Assert.Equal("Invalid token.", exception.Message);
    }

    [Fact]
    public async Task RefreshTokenAsync_DbRefreshTokenExpired_ThrowsUnauthorizedAccessException()
    {
        var user = new User
        {
            Id = 1, Email = "test@example.com", PasswordHash = "pwd", UserRole = UserRole.DefaultUser,
            IsBanned =false
        };
        var expiredDbRefreshToken = new RefreshToken
        {
            Token = "expiredRefreshToken", UserId = user.Id, ExpiresOnUtc = DateTime.UtcNow.AddHours(-1), User = user
        }; // Expired
        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id, default, null)).ReturnsAsync(user);
        _refreshTokenRepoMock
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<RefreshToken, bool>>>(), default))
            .ReturnsAsync(expiredDbRefreshToken);
        var service = CreateService();
        var validJwt = GenerateTestJwtTokenString(user.Id, user.Email, user.UserRole, DateTime.UtcNow.AddMinutes(5),
            _jwtSettings.Key, _jwtSettings.Issuer, _jwtSettings.Audience);

        var exception =
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                service.RefreshTokenAsync(validJwt, "expiredRefreshToken"));
        Assert.Equal("Invalid token.", exception.Message);
    }
}