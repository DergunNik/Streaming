using AuthServerApp;
using AuthService.Data;
using AuthService.Models;
using AuthService.Services.HelpersImplementations;
using AuthService.Services.HelpersInterfaces;
using AuthService.Settings;
using EmailClientApp;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Google.Protobuf.WellKnownTypes;

namespace AuthTests;

public class AuthServiceTests
{
    private readonly Mock<IOptions<AuthSettings>> _authSettingsMock = new();
    private readonly Mock<EmailService.EmailServiceClient> _emailServiceClientMock = new();
    private readonly Mock<IOptions<EncryptionSettings>> _encryptionSettingsMock = new();
    private readonly Mock<IHashService> _hashServiceMock = new();
    private readonly Mock<IJwtService> _jwtServiceMock = new();
    private readonly Mock<ILogger<JwtService>> _loggerMock = new();
    private readonly Mock<IRepository<RefreshToken>> _refreshTokensRepositoryMock = new();
    private readonly Mock<IRepository<UserRegRequest>> _requestsRepositoryMock = new();
    private readonly Mock<IRepository<User>> _usersRepositoryMock = new();
    private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor = new();
    private readonly DefaultHttpContext _httpContext = new();

    public AuthServiceTests()
    {
        _authSettingsMock.Setup(a => a.Value).Returns(new AuthSettings
        {
            MinPasswordSize = 5,
            PasswordSize = 20,
            EmailSize = 50,
            RegistrationCodeSize = 10,
            RegistrationCodeLifetimeMinutes = 30,
            AccessTokenLifetime = TimeSpan.FromMinutes(10),
            RefreshTokenLifetime = TimeSpan.FromHours(1)
        });
        _encryptionSettingsMock.Setup(e => e.Value).Returns(new EncryptionSettings
        {
            SaltSize = 16,
            BdHashSize = 256,
            RefreshTokenSize = 32
        });

        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(_httpContext);
    }

    private void SetupUserContext(int userId, string role = "DefaultUser")
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims);
        var principal = new ClaimsPrincipal(identity);
        _httpContext.User = principal;
    }

    private AuthService.Services.AuthService CreateService()
    {
        return new AuthService.Services.AuthService(
            _loggerMock.Object,
            _hashServiceMock.Object,
            _usersRepositoryMock.Object,
            _requestsRepositoryMock.Object,
            _refreshTokensRepositoryMock.Object,
            _jwtServiceMock.Object,
            _authSettingsMock.Object,
            _encryptionSettingsMock.Object,
            _emailServiceClientMock.Object,
            _mockHttpContextAccessor.Object
        );
    }

    [Fact]
    public async Task Login_ValidRequest_ShouldReturnToken()
    {
        var request = new LoginRequest { Email = "test@example.com", Password = "testPass" };
        _jwtServiceMock.Setup(js => js.GenerateTokensAsync(request.Email, request.Password))
            .ReturnsAsync(("jwtToken", "refreshToken"));
        var service = CreateService();

        var result = await service.Login(request, default);

        Assert.Equal("jwtToken", result.JwtToken);
        Assert.Equal("refreshToken", result.RefreshToken);
    }

    [Fact]
    public async Task Login_InvalidCredentials_ShouldThrowRpcException()
    {
        var request = new LoginRequest { Email = "test@example.com", Password = "wrongPass" };
        _jwtServiceMock.Setup(js => js.GenerateTokensAsync(request.Email, request.Password))
            .ThrowsAsync(new ArgumentException("Wrong email or password."));
        var service = CreateService();

        await Assert.ThrowsAsync<RpcException>(() => service.Login(request, default));
    }

    [Fact]
    public async Task BeginRegistration_InvalidEmail_ShouldThrowRpcException()
    {
        var request = new RegisterRequest { Email = "invalidEmail", Password = "pass123" };
        var service = CreateService();

        await Assert.ThrowsAsync<RpcException>(() => service.BeginRegistration(request, default));
    }

    [Fact]
    public async Task BeginRegistration_TooShortPassword_ShouldThrowRpcException()
    {
        var request = new RegisterRequest { Email = "test@mail.com", Password = "1234" };
        var service = CreateService();

        await Assert.ThrowsAsync<RpcException>(() => service.BeginRegistration(request, default));
    }

    [Fact]
    public async Task FinishRegistration_MissingCode_ShouldThrowRpcException()
    {
        var request = new FinishRequest { Email = "test@mail.com", Code = "" };
        var service = CreateService();

        await Assert.ThrowsAsync<RpcException>(() => service.FinishRegistration(request, default));
    }

    [Fact]
    public async Task Refresh_ValidToken_ShouldReturnNewTokens()
    {
        _jwtServiceMock.Setup(js => js.RefreshTokenAsync("oldJwt", "oldRefresh"))
            .ReturnsAsync(("newJwt", "newRefresh"));
        var request = new RefreshRequest { JwtToken = "oldJwt", RefreshToken = "oldRefresh" };
        var service = CreateService();

        var result = await service.Refresh(request, default);

        Assert.Equal("newJwt", result.JwtToken);
        Assert.Equal("newRefresh", result.RefreshToken);
    }

    [Fact]
    public async Task Refresh_ExpiredRefreshToken_ShouldThrowRpcException()
    {
        _jwtServiceMock.Setup(js => js.RefreshTokenAsync("jwt", "expiredRefresh"))
            .ThrowsAsync(new SecurityTokenException("Invalid or expired refresh token."));
        var request = new RefreshRequest { JwtToken = "jwt", RefreshToken = "expiredRefresh" };
        var service = CreateService();

        await Assert.ThrowsAsync<RpcException>(() => service.Refresh(request, default));
    }

    [Fact]
    public async Task Logout_InvalidEmail_ShouldThrowRpcException()
    {
        // SetupUserContext(1);
        var service = CreateService();
        
        await Assert.ThrowsAsync<RpcException>(() => service.Logout(new Empty(), default));
    }
}