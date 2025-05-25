using Account;
using AccountService.Data;
using AccountService.Settings;
using CloudinaryDotNet;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using AccountInfo = AccountService.Models.AccountInfo;

namespace AccountService.UnitTests;

public class AccountServiceTests
{
    private readonly Services.AccountService _accountService;
    private readonly Cloudinary _cloudinary;
    private readonly AppDbContext _dbContext;
    private readonly Mock<ServerCallContext> _mockServerCallContext = new();

    public AccountServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"AccountsTestDb_{Guid.NewGuid()}")
            .Options;

        var cloudinaryRestrictions = Options.Create(new CloudinaryRestrictions
        {
            PublicIdMaxSize = 100
        });
        var contentRestrictions = Options.Create(new ContentRestrictions
        {
            MaxDescriptionLength = 200,
            MaxImageSizeBytes = 200 * 1024,
            AvatarAspectRatioMin = 0.8,
            AvatarAspectRatioMax = 1.2,
            BackgroundAspectRatioMin = 1.6,
            BackgroundAspectRatioMax = 1.8
        });
        var dbCredentials = Options.Create(new DbCredentials
        {
            Host = "localhost",
            Port = "5432",
            Db = "test",
            User = "testuser",
            Password = "testpass"
        });

        _dbContext = new AppDbContext(options, cloudinaryRestrictions, contentRestrictions, dbCredentials);
        var cloudAccount = new CloudinaryDotNet.Account("your-cloud", "your-api-key", "your-api-secret");
        _cloudinary = new Cloudinary(cloudAccount);
        _accountService = new Services.AccountService(_dbContext, _cloudinary, contentRestrictions);
    }

    [Fact]
    public async Task CreateAccount_ShouldSucceed_WhenUserDoesNotExist()
    {
        var request = new CreateAccountRequest
        {
            Info = new SetAccountInfo
            {
                UserId = 1,
                Description = "Test description"
            }
        };

        var response = await _accountService.CreateAccount(request, _mockServerCallContext.Object);

        Assert.NotNull(response.Info);
        Assert.Equal(1, response.Info.UserId);
        Assert.False(response.Info.IsBanned);
        Assert.Equal("Test description", response.Info.Description);
    }

    [Fact]
    public async Task CreateAccount_ShouldThrowAlreadyExists_WhenUserExists()
    {
        await _dbContext.Accounts.AddAsync(new AccountInfo
        {
            UserId = 10,
            Description = "Existing user"
        });
        await _dbContext.SaveChangesAsync();

        var request = new CreateAccountRequest
        {
            Info = new SetAccountInfo
            {
                UserId = 10,
                Description = "New user data"
            }
        };

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            _accountService.CreateAccount(request, _mockServerCallContext.Object));

        Assert.Equal(StatusCode.AlreadyExists, ex.StatusCode);
    }

    [Fact]
    public async Task GetAccount_ShouldReturnAccount_WhenUserExists()
    {
        const int userId = 100;
        await _dbContext.Accounts.AddAsync(new AccountInfo
        {
            UserId = userId,
            Description = "Dummy account"
        });
        await _dbContext.SaveChangesAsync();

        var request = new GetAccountRequest { UserId = userId };

        var response = await _accountService.GetAccount(request, _mockServerCallContext.Object);

        Assert.NotNull(response.Info);
        Assert.Equal(userId, response.Info.UserId);
        Assert.Equal("Dummy account", response.Info.Description);
    }

    [Fact]
    public async Task GetAccount_ShouldThrowNotFound_WhenUserDoesNotExist()
    {
        var request = new GetAccountRequest { UserId = 999 };

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            _accountService.GetAccount(request, _mockServerCallContext.Object));
        Assert.Equal(StatusCode.NotFound, ex.StatusCode);
    }

    [Fact]
    public async Task SetBanStatus_ShouldBanUser_WhenCalled()
    {
        var userId = 200;
        await _dbContext.Accounts.AddAsync(new AccountInfo
        {
            UserId = userId,
            Description = "To be banned"
        });
        await _dbContext.SaveChangesAsync();

        var banRequest = new AccountBanRequest
        {
            UserId = userId,
            IsBanned = true
        };

        await _accountService.SetBanStatus(banRequest, _mockServerCallContext.Object);
        var user = await _dbContext.Accounts.FindAsync(userId);

        Assert.True(user.IsBanned);
    }

    [Fact]
    public async Task UpdateAccount_ShouldMaintainBanStatus_WhenAlreadyBanned()
    {
        var userId = 300;
        await _dbContext.Accounts.AddAsync(new AccountInfo
        {
            UserId = userId,
            IsBanned = true,
            Description = "Banned user"
        });
        await _dbContext.SaveChangesAsync();

        var updateRequest = new UpdateAccountRequest
        {
            Info = new SetAccountInfo
            {
                UserId = userId,
                Description = "Updated data"
            }
        };

        var reply = await _accountService.UpdateAccount(updateRequest, _mockServerCallContext.Object);

        Assert.NotNull(reply.Info);
        Assert.Equal(userId, reply.Info.UserId);
        Assert.True(reply.Info.IsBanned);
        Assert.Empty(reply.Info.Description);
    }

    [Fact]
    public async Task UpdateAccount_ShouldUpdateFields_WhenUserIsNotBanned()
    {
        var userId = 400;
        await _dbContext.Accounts.AddAsync(new AccountInfo
        {
            UserId = userId,
            Description = "Initial data",
            IsBanned = false
        });
        await _dbContext.SaveChangesAsync();

        var updateRequest = new UpdateAccountRequest
        {
            Info = new SetAccountInfo
            {
                UserId = userId,
                Description = "New description"
            }
        };

        var reply = await _accountService.UpdateAccount(updateRequest, _mockServerCallContext.Object);

        Assert.NotNull(reply.Info);
        Assert.Equal("New description", reply.Info.Description);
    }
}