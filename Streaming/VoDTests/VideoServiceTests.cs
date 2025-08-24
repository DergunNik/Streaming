using AuthService.Settings;
using CloudinaryDotNet;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using VoD;
using VoDService.Data;
using VoDService.Settings;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using VideoInfo = VoDService.Models.VideoInfo;
using VideoService = VoDService.Services.VideoService;

namespace VoDService.UnitTests;

public class VideoServiceTests
{
    private readonly Cloudinary _cloudinary;
    private readonly AppDbContext _dbContext;
    private readonly Mock<ServerCallContext> _mockServerCallContext = new();
    private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor = new();
    private readonly DefaultHttpContext _httpContext = new();
    private readonly VideoService _videoService;

    public VideoServiceTests()
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"VoDTestDb_{Guid.NewGuid()}")
            .Options;

        var cloudinaryRestrictions = Options.Create(new CloudinaryRestrictions
        {
            PublicIdMaxSize = 100
        });
        var dbCredentials = Options.Create(new DbCredentials
        {
            Host = "localhost",
            Port = "5432",
            Db = "testdb",
            User = "testuser",
            Password = "testpass"
        });

        _dbContext = new AppDbContext(dbOptions, cloudinaryRestrictions, dbCredentials);

        var account = new Account("1", "2", "3");
        _cloudinary = new Cloudinary(account);

        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(_httpContext);
        _videoService = new VideoService(_cloudinary, _dbContext, _mockHttpContextAccessor.Object);
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

    [Fact]
    public async Task UploadVideo_ShouldThrow_WhenNoSourceProvided()
    {
        const int userId = 1;
        SetupUserContext(userId);

        var request = new UploadVideoRequest
        {
            Title = "No Source Video",
            Description = "Missing data and URI"
        };

        var ex = await Assert.ThrowsAsync<RpcException>(
            () => _videoService.UploadVideo(request, _mockServerCallContext.Object));
        Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
    }

    [Fact]
    public async Task UploadVideo_ShouldThrow_WhenWrongUriProvided()
    {
        const int userId = 123;
        SetupUserContext(userId);

        var request = new UploadVideoRequest
        {
            Uri = "http://example.com/sample.mp4",
            Title = "Test Video Title",
            Description = "Test Video Description",
            Tags = { "tag1", "tag2" }
        };

        await Assert.ThrowsAsync<RpcException>(async () => await _videoService.UploadVideo(request, _mockServerCallContext.Object));
    }

    [Fact]
    public async Task GetVideoManifest_ShouldReturnHlsUrl_WhenTypeIsHls()
    {
        SetupUserContext(1);

        var request = new GetManifestRequest
        {
            PublicId = "test_public_id",
            Type = GetManifestRequest.Types.ManifestType.Hls
        };

        var reply = await _videoService.GetVideoManifest(request, _mockServerCallContext.Object);

        Assert.Contains(".m3u8", reply.ManifestUrl);
    }

    [Fact]
    public async Task GetVideoUrl_ShouldReturnTransformedUrl_WhenTransformationIsProvided()
    {
        SetupUserContext(1);

        var request = new GetUrlRequest
        {
            PublicId = "test_public_id",
            Format = "mp4",
            Transformation = "w_400,h_300,c_fill"
        };

        var reply = await _videoService.GetVideoUrl(request, _mockServerCallContext.Object);

        Assert.NotNull(reply.Url);
        Assert.Contains("test_public_id.mp4", reply.Url);
    }

    [Fact]
    public async Task GetVideosOfUser_ShouldReturnListOfUsersVideos()
    {
        const int userId = 555;
        SetupUserContext(userId);

        var video1 = new VideoInfo { PublicId = "v1", LikeCount = 2, DislikeCount = 1 };
        var video2 = new VideoInfo { PublicId = "v2", LikeCount = 1, DislikeCount = 0 };
        await _dbContext.Videos.AddRangeAsync(video1, video2);
        await _dbContext.SaveChangesAsync();

        var request = new GetVideosOfUserRequest
        {
            UserId = userId,
            PageSize = 2
        };

        var reply = await _videoService.GetVideosOfUser(request, _mockServerCallContext.Object);

        Assert.NotNull(reply);
    }

    [Fact]
    public async Task ListVideos_ShouldReturnProperlyOrderedVideos()
    {
        SetupUserContext(1);

        var videoOne = new VideoInfo { PublicId = "vid_one", LikeCount = 0, DislikeCount = 0 };
        var videoTwo = new VideoInfo { PublicId = "vid_two", LikeCount = 0, DislikeCount = 0 };
        await _dbContext.Videos.AddRangeAsync(videoOne, videoTwo);
        await _dbContext.SaveChangesAsync();

        var request = new ListVideosRequest
        {
            PageSize = 2
        };

        var reply = await _videoService.ListVideos(request, _mockServerCallContext.Object);

        Assert.NotNull(reply);
    }

    private class TestServerCallContext : ServerCallContext
    {
        private readonly CancellationToken _cancellationToken;

        public TestServerCallContext(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
        }

        protected override string MethodCore => "TestMethod";
        protected override string HostCore => "localhost";
        protected override string PeerCore => "localuser";
        protected override DateTime DeadlineCore => DateTime.UtcNow.AddMinutes(5);
        protected override Metadata RequestHeadersCore => new();
        protected override Metadata ResponseTrailersCore { get; } = new();
        protected override Status StatusCore { get; set; }
        protected override WriteOptions WriteOptionsCore { get; set; }

        protected override AuthContext AuthContextCore => new("test", new Dictionary<string, List<AuthProperty>>());

        protected override CancellationToken CancellationTokenCore => _cancellationToken;
        protected override IDictionary<object, object> UserStateCore { get; } = new Dictionary<object, object>();

        protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions options)
        {
            return null;
        }

        protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders)
        {
            return Task.CompletedTask;
        }
    }
}