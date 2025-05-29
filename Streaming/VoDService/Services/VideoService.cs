using System.Security.Claims;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using VoD;
using VoDService.Data;
using VoDService.Models;
using VideoInfo = VoD.VideoInfo;

namespace VoDService.Services;

public class VideoService : VoD.VideoService.VideoServiceBase
{
    private readonly Cloudinary _cloudinary;
    private readonly AppDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public VideoService(Cloudinary cloudinary, AppDbContext dbContext, IHttpContextAccessor httpContextAccessor)
    {
        _cloudinary = cloudinary;
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
    }

    [Authorize(Roles = "DefaultUser,Admin")]
    public override async Task<UploadVideoReply> UploadVideo(UploadVideoRequest request, ServerCallContext context)
    {
        var userId = GetUserId();
        var uploadParams = new VideoUploadParams
        {
            Tags = string.Join(",", request.Tags),
            Context = new StringDictionary
            {
                { "title", request.Title },
                { "description", request.Description }
            },
            MetadataFields = new StringDictionary
            {
                { "author", userId.ToString() }
            }
        };

        Stream? dataStream = null;

        switch (request.SourceCase)
        {
            case UploadVideoRequest.SourceOneofCase.Uri:
                uploadParams.File = new FileDescription(request.Uri);
                break;

            case UploadVideoRequest.SourceOneofCase.Data:
            {
                var bytes = request.Data.ToByteArray();
                dataStream = new MemoryStream(bytes);
                uploadParams.File = new FileDescription("buffered_video", dataStream);
                break;
            }

            case UploadVideoRequest.SourceOneofCase.None:
            default:
                throw new RpcException(new Status(StatusCode.InvalidArgument, "No video source provided"));
        }

        var result = await _cloudinary.UploadAsync(uploadParams);

        dataStream?.Dispose();

        return new UploadVideoReply
        {
            PublicId = result.PublicId ??
                       throw new RpcException(new Status(StatusCode.InvalidArgument, "No video source provided")),
            SecureUrl = result.SecureUrl.ToString(),
            UploadedAt = Timestamp.FromDateTime(result.CreatedAt.ToUniversalTime())
        };
    }

    [Authorize]
    public override async Task<ArchiveStreamReply> ArchiveStream(ArchiveStreamRequest request, ServerCallContext context)
    {
        var userId = GetUserId();
    
        var existingResource = await _cloudinary.GetResourceAsync(new GetResourceParams(request.PublicStreamId)
        {
            ResourceType = ResourceType.Video
        });

        if (existingResource.Metadata["author"] != userId.ToString())
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "User cant archive stream of other person"));
        }
        
        if (existingResource is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Stream not found"));
        }

        var updateParams = new UpdateParams(request.PublicStreamId)
        {
            ResourceType = ResourceType.Video,
            Tags = string.Join(",", request.Tags),
            Context = new StringDictionary
            {
                { "title", request.Title },
                { "description", request.Description }
            },
            Metadata = new StringDictionary
            {
                { "author", userId.ToString() }
            }
        };

        var updateResult = await _cloudinary.UpdateResourceAsync(updateParams);
        
        var video = new Models.VideoInfo
        {
            PublicId = request.PublicStreamId,
            LikeCount = 0,
            DislikeCount = 0
        };
        await _dbContext.Videos.AddAsync(video, context.CancellationToken);

        await _dbContext.SaveChangesAsync(context.CancellationToken);

        return new ArchiveStreamReply
        {
            PublicId = request.PublicStreamId,
            SecureUrl = updateResult.SecureUrl,
            UploadedAt = Timestamp.FromDateTime(DateTime.UtcNow)
        };
    }

    public override Task<GetManifestReply> GetVideoManifest(GetManifestRequest request, ServerCallContext context)
    {
        var ext = request.Type == GetManifestRequest.Types.ManifestType.Hls ? "m3u8" : "mpd";
        var url = _cloudinary.Api.UrlVideoUp
            .Transform(new Transformation().StreamingProfile("auto:maxres_720"))
            .Format(ext)
            .BuildUrl($"{request.PublicId}.{ext}");
        return Task.FromResult(new GetManifestReply { ManifestUrl = url });
    }

    public override Task<GetUrlReply> GetVideoUrl(GetUrlRequest request, ServerCallContext context)
    {
        var urlBuilder = _cloudinary.Api.UrlVideoUp;
        if (!string.IsNullOrEmpty(request.Transformation))
            urlBuilder.Transform(new Transformation().RawTransformation(request.Transformation));
        var ext = string.IsNullOrEmpty(request.Format) ? "" : "." + request.Format;
        var fullPublicId = request.PublicId + ext;
        var url = urlBuilder.BuildUrl(fullPublicId);
        return Task.FromResult(new GetUrlReply { Url = url });
    }

    public override async Task<GetVideosOfUserReply> GetVideosOfUser(GetVideosOfUserRequest request,
        ServerCallContext context)
    {
        var pageSize = request.PageSize > 0 ? request.PageSize : 10;
        var expr = $"resource_type:video AND metadata.author=\"{request.UserId}\"";
        var searchBuilder = _cloudinary
            .Search()
            .Expression(expr)
            .MaxResults(pageSize);

        if (!string.IsNullOrEmpty(request.PageToken)) searchBuilder = searchBuilder.NextCursor(request.PageToken);

        var result = await Task.Run(() => searchBuilder.Execute());
        var publicIds = result.Resources.Select(r => r.PublicId).ToList();

        var infos = await _dbContext.Videos
            .AsNoTracking()
            .Where(v => publicIds.Contains(v.PublicId))
            .Select(v => new VideoInfo
            {
                PublicId = v.PublicId,
                Likes = v.LikeCount,
                Dislikes = v.DislikeCount
            })
            .ToListAsync();

        var reply = new GetVideosOfUserReply
        {
            NextCursor = result.NextCursor ?? string.Empty,
            Videos = { infos }
        };

        return reply;
    }

    [Authorize(Roles = "DefaultUser,Admin")]
    public override async Task<SetReactionReply> SetReaction(
        SetReactionRequest request,
        ServerCallContext context)
    {
        await using var tx = await _dbContext.Database.BeginTransactionAsync();

        var video = await _dbContext.Videos.FirstOrDefaultAsync(
            v => v.PublicId == request.PublicId, context.CancellationToken);

        var userId = GetUserId();
        var reaction = await _dbContext.Reactions.FirstOrDefaultAsync(
            r => r.PublicId == request.PublicId && r.UserId == userId,
            context.CancellationToken);

        if (video is null)
            throw new RpcException(new Status(StatusCode.NotFound, $"Video '{request.PublicId}' not found."));

        if (reaction is null && request.Reaction != ReactionType.ReactionUnspecified)
        {
            var newReaction = new Reaction
            {
                PublicId = request.PublicId,
                UserId = userId,
                IsLike = request.Reaction == ReactionType.Like
            };
            await _dbContext.Reactions.AddAsync(newReaction, context.CancellationToken);

            if (newReaction.IsLike)
                video.LikeCount++;
            else
                video.DislikeCount++;
        }
        else if (reaction is not null &&
                 !((request.Reaction == ReactionType.Like && reaction.IsLike) ||
                   (request.Reaction == ReactionType.Dislike && !reaction.IsLike)))
        {
            switch (request.Reaction)
            {
                default:
                case ReactionType.ReactionUnspecified:
                    _dbContext.Reactions.Remove(reaction);
                    if (reaction.IsLike)
                        video.LikeCount--;
                    else
                        video.DislikeCount--;
                    break;
                case ReactionType.Like:
                    reaction.IsLike = true;
                    video.LikeCount++;
                    video.DislikeCount--;
                    break;
                case ReactionType.Dislike:
                    reaction.IsLike = false;
                    video.LikeCount--;
                    video.DislikeCount++;
                    break;
            }
        }

        await _dbContext.SaveChangesAsync(context.CancellationToken);
        await tx.CommitAsync(context.CancellationToken);

        var info = new VideoInfo
        {
            PublicId = video.PublicId,
            Likes = video.LikeCount,
            Dislikes = video.DislikeCount
        };

        return new SetReactionReply { Info = info };
    }

    public override async Task<ListVideosReply> ListVideos(
        ListVideosRequest request,
        ServerCallContext context)
    {
        var pageSize = request.PageSize > 0 ? request.PageSize : 10;
        var expr = "resource_type:video";

        if (!string.IsNullOrWhiteSpace(request.Filter)) expr += $" AND {request.Filter}";

        var searchBuilder = _cloudinary.Search()
            .Expression(expr)
            .MaxResults(pageSize);

        if (!string.IsNullOrEmpty(request.PageToken)) searchBuilder = searchBuilder.NextCursor(request.PageToken);

        var result = await searchBuilder.ExecuteAsync();

        var publicIds = result.Resources.Select(r => r.PublicId).ToList();

        var videos = await _dbContext.Videos
            .Where(v => publicIds.Contains(v.PublicId))
            .ToListAsync(context.CancellationToken);

        var videoDict = videos.ToDictionary(v => v.PublicId);
        var orderedVideos = publicIds
            .Where(id => videoDict.ContainsKey(id))
            .Select(id => videoDict[id])
            .ToList();

        var reply = new ListVideosReply
        {
            NextCursor = result.NextCursor ?? string.Empty
        };

        foreach (var video in orderedVideos)
            reply.Videos.Add(new VideoInfo
            {
                PublicId = video.PublicId,
                Likes = video.LikeCount,
                Dislikes = video.DislikeCount
            });

        return reply;
    }

    [Authorize]
    public override async Task<Empty> DeleteVideo(DeleteVideoRequest request, ServerCallContext context)
    {
        var userId = GetUserId();
        var userRole = GetUserRole();

        var authorId = await GetAuthorIdFromCloudinary(request.PublicId);

        if (authorId != userId && userRole == "DefaultUser")
            throw new RpcException(new Status(StatusCode.PermissionDenied,
                "You do not have permission to delete this video."));

        var deletionParams = new DeletionParams(request.PublicId)
        {
            ResourceType = ResourceType.Video,
            Invalidate = true
        };

        await _cloudinary.DestroyAsync(deletionParams);

        await using var tx = await _dbContext.Database.BeginTransactionAsync();
        var video = await _dbContext.Videos.FirstOrDefaultAsync(e => e.PublicId == request.PublicId,
            context.CancellationToken);

        if (video is not null)
        {
            _dbContext.Videos.Remove(video);
            await _dbContext.SaveChangesAsync(context.CancellationToken);
            await _dbContext.Database.CommitTransactionAsync();
        }

        return new Empty();
    }

    private int GetUserId()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var userId = int.Parse(httpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                               throw new RpcException(new Status(StatusCode.Unauthenticated, "Unauthenticated")));
        return userId;
    }

    private string GetUserRole()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var role = httpContext?.User?.FindFirst(ClaimTypes.Role)?.Value ??
                   throw new RpcException(new Status(StatusCode.Unauthenticated, "Unauthenticated"));
        return role;
    }

    private async Task<int> GetAuthorIdFromCloudinary(string publicId)
    {
        var resource = await _cloudinary.GetResourceAsync(new GetResourceParams(publicId)
        {
            ResourceType = ResourceType.Video
        });
        if (resource.ImageMetadata != null &&
            resource.ImageMetadata.TryGetValue("author", out var authorIdStr) &&
            int.TryParse(authorIdStr, out var authorId))
            return authorId;
        throw new RpcException(new Status(StatusCode.NotFound, "Author not found in video metadata"));
    }
}