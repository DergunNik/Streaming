using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuthService.Settings;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using VoD;
using VoDService.Services;
using VoDService.Data;
using VoDService.Settings;
using Xunit;

namespace VoDService.UnitTests
{
    public class VideoServiceTests
    {
        private readonly AppDbContext _dbContext;
        private readonly Cloudinary _cloudinary;
        private readonly Mock<ServerCallContext> _mockServerCallContext = new();
        private readonly VoDService.Services.VideoService _videoService;

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

            var account = new Account("diw0s5viu", "747123821559715", "VTzfVgxcX3_XEJJezm-FVQrDJuA");
            _cloudinary = new Cloudinary(account);

            _videoService = new VoDService.Services.VideoService(_cloudinary, _dbContext);
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
            protected override Metadata RequestHeadersCore => new Metadata();
            protected override Metadata ResponseTrailersCore { get; } = new Metadata();
            protected override Status StatusCore { get; set; }
            protected override WriteOptions WriteOptionsCore { get; set; }

            protected override AuthContext AuthContextCore =>
                new AuthContext("test", new Dictionary<string, List<AuthProperty>>());

            protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions options) =>
                null;

            protected override CancellationToken CancellationTokenCore => _cancellationToken;
            protected override IDictionary<object, object> UserStateCore { get; } = new Dictionary<object, object>();
            protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
        }

        [Fact]
        public async Task UploadVideo_ShouldThrow_WhenNoSourceProvided()
        {
            var request = new UploadVideoRequest
            {
                Title = "No Source Video",
                Description = "Missing data and URI"
            };
            var context = new TestServerCallContext(CancellationToken.None);

            var ex = await Assert.ThrowsAsync<RpcException>(
                () => _videoService.UploadVideo(request, context));
            Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
        }

        [Fact]
        public async Task UploadVideo_ShouldThrow_WhenWrongUriProvided()
        {
            var request = new UploadVideoRequest
            {
                Uri = "http://example.com/sample.mp4",
                Title = "Test Video Title",
                Description = "Test Video Description",
                AuthorId = 123,
                Tags = { "tag1", "tag2" }
            };
            var context = new TestServerCallContext(CancellationToken.None);
            
            await Assert.ThrowsAsync<RpcException>(async () => await _videoService.UploadVideo(request, context));
        }

        [Fact]
        public async Task GetVideoManifest_ShouldReturnHlsUrl_WhenTypeIsHls()
        {
            var context = new TestServerCallContext(CancellationToken.None);
            var request = new GetManifestRequest
            {
                PublicId = "test_public_id",
                Type = GetManifestRequest.Types.ManifestType.Hls
            };

            var reply = await _videoService.GetVideoManifest(request, context);

            Assert.Contains(".m3u8", reply.ManifestUrl);
        }

        [Fact]
        public async Task GetVideoUrl_ShouldReturnTransformedUrl_WhenTransformationIsProvided()
        {
            var context = new TestServerCallContext(CancellationToken.None);
            var request = new GetUrlRequest
            {
                PublicId = "test_public_id",
                Format = "mp4",
                Transformation = "w_400,h_300,c_fill"
            };

            var reply = await _videoService.GetVideoUrl(request, context);

            Assert.NotNull(reply.Url);
            Assert.Contains("test_public_id.mp4", reply.Url);
        }
        
        [Fact]
        public async Task GetVideosOfUser_ShouldReturnListOfUsersVideos()
        {
            var context = new TestServerCallContext(CancellationToken.None);

            var video1 = new Models.VideoInfo { PublicId = "v1", LikeCount = 2, DislikeCount = 1 };
            var video2 = new Models.VideoInfo { PublicId = "v2", LikeCount = 1, DislikeCount = 0 };
            await _dbContext.Videos.AddRangeAsync(video1, video2);
            await _dbContext.SaveChangesAsync();

            var request = new GetVideosOfUserRequest
            {
                UserId = 555,
                PageSize = 2
            };
            
            var reply = await _videoService.GetVideosOfUser(request, context);

            Assert.NotNull(reply);
        }

        [Fact]
        public async Task ListVideos_ShouldReturnProperlyOrderedVideos()
        {
            var context = new TestServerCallContext(CancellationToken.None);

            var videoOne = new Models.VideoInfo { PublicId = "vid_one", LikeCount = 0, DislikeCount = 0 };
            var videoTwo = new Models.VideoInfo { PublicId = "vid_two", LikeCount = 0, DislikeCount = 0 };
            await _dbContext.Videos.AddRangeAsync(videoOne, videoTwo);
            await _dbContext.SaveChangesAsync();

            var request = new ListVideosRequest
            {
                PageSize = 2
            };

            var reply = await _videoService.ListVideos(request, context);

            Assert.NotNull(reply);
        }
    }
}