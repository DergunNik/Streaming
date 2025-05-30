﻿using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using CloudinaryUtils.Settings;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using LiveService.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Streaming;
using HttpMethod = System.Net.Http.HttpMethod;
using StreamInfo = LiveService.Models.StreamInfo;

namespace LiveService.Services;

public class StreamService : Streaming.StreamService.StreamServiceBase
{
    private readonly AppDbContext _db;
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly CloudinarySettings _settings;
    private readonly VoD.VideoService.VideoServiceClient _videoServiceClient;
    
    public StreamService(
        IOptions<CloudinarySettings> cloudinarySettings,
        AppDbContext dbContext,
        IHttpContextAccessor httpContextAccessor,
        VoD.VideoService.VideoServiceClient videoServiceClient)
    {
        _settings = cloudinarySettings.Value;
        _httpClient = new HttpClient();
        _db = dbContext;
        _httpContextAccessor = httpContextAccessor;
        _videoServiceClient = videoServiceClient;

        var byteArray = Encoding.ASCII.GetBytes($"{_settings.ApiKey}:{_settings.ApiSecret}");
        _httpClient.DefaultRequestHeaders.Authorization
            = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
    }

    private string BaseUrl => $"https://api.cloudinary.com/v2/video/{_settings.CloudName}";

    [Authorize]
    public override async Task<StreamDetailedReply> CreateStream(CreateStreamRequest request, ServerCallContext context)
    {
        var bodyObj = new
        {
            name = request.Name,
            input = new { type = "rtmp" },
            idle_timeout_sec = request.HasIdleTimeoutSec ? request.IdleTimeoutSec : (int?)null,
            max_runtime_sec = request.HasMaxRuntimeSec ? request.MaxRuntimeSec : (int?)null
        };
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/live_streams")
        {
            Content = new StringContent(JsonSerializer.Serialize(bodyObj), Encoding.UTF8, "application/json")
        };

        var response = await _httpClient.SendAsync(httpRequest);

        var (id, name, status, createdAt, updatedAt, data) = await ParseStreamData(response);
        var archivePublicId = ExtractArchivePublicId(data);
        var userId = GetUserId();
        var newStream = new StreamInfo
        {
            AuthorId = userId,
            CloudinaryStreamId = id,
            Name = name,
            ArchivePublicId = archivePublicId
        };
        await _db.Streams.AddAsync(newStream);
        await _db.SaveChangesAsync();

        var reply = new StreamDetailedReply
        {
            CloudinaryStreamId = id,
            Name = name,
            AuthorId = userId,
            Status = status,
            CloudinaryIdleTimeoutSec = request.HasIdleTimeoutSec ? request.IdleTimeoutSec : 0,
            CloudinaryMaxRuntimeSec = request.HasMaxRuntimeSec ? request.MaxRuntimeSec : 0,
            CreatedAt = Timestamp.FromDateTime(createdAt),
            UpdatedAt = Timestamp.FromDateTime(updatedAt)
        };
        return reply;
    }

    [Authorize]
    public override async Task<StreamDetailedReply> GetStream(GetStreamRequest request, ServerCallContext context)
    {
        var author = await GetAuthorOrThrowIfBadAsync(request.CloudinaryStreamId);
        var httpRequest =
            new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/live_streams/{request.CloudinaryStreamId}");
        var response = await _httpClient.SendAsync(httpRequest);
        var (id, name, status, createdAt, updatedAt, _) = await ParseStreamData(response);
        var reply = new StreamDetailedReply
        {
            CloudinaryStreamId = id,
            Name = name,
            AuthorId = author,
            Status = status,
            CreatedAt = Timestamp.FromDateTime(createdAt),
            UpdatedAt = Timestamp.FromDateTime(updatedAt)
        };
        return reply;
    }

    [Authorize]
    public override async Task<StreamDetailedReply> UpdateStream(UpdateStreamRequest request, ServerCallContext context)
    {
        var author = await GetAuthorOrThrowIfBadAsync(request.CloudinaryStreamId);
        var bodyObj = new
        {
            name = request.HasName ? request.Name : null,
            idle_timeout_sec = request.HasIdleTimeoutSec ? request.IdleTimeoutSec : (int?)null,
            max_runtime_sec = request.HasMaxRuntimeSec ? request.MaxRuntimeSec : (int?)null
        };
        var httpRequest =
            new HttpRequestMessage(HttpMethod.Patch, $"{BaseUrl}/live_streams/{request.CloudinaryStreamId}")
            {
                Content = new StringContent(JsonSerializer.Serialize(bodyObj), Encoding.UTF8, "application/json")
            };
        var response = await _httpClient.SendAsync(httpRequest);
        var (id, name, status, createdAt, updatedAt, data) = await ParseStreamData(response);
        var archivePublicId = ExtractArchivePublicId(data);
        var dbStream = await _db.Streams.FirstOrDefaultAsync(x => x.CloudinaryStreamId == id);
        if (dbStream is not null)
        {
            dbStream.Name = name;
            if (!string.IsNullOrEmpty(archivePublicId)) dbStream.ArchivePublicId = archivePublicId;
            _db.Streams.Update(dbStream);
            await _db.SaveChangesAsync();
        }

        return new StreamDetailedReply
        {
            CloudinaryStreamId = id,
            Name = name,
            AuthorId = author,
            Status = status,
            CreatedAt = Timestamp.FromDateTime(createdAt),
            UpdatedAt = Timestamp.FromDateTime(updatedAt)
        };
    }

    [Authorize]
    public override async Task<Empty> DeleteStream(DeleteStreamRequest request, ServerCallContext context)
    {
        await GetAuthorOrThrowIfBadAsync(request.CloudinaryStreamId);
        var httpRequest =
            new HttpRequestMessage(HttpMethod.Delete, $"{BaseUrl}/live_streams/{request.CloudinaryStreamId}");
        var response = await _httpClient.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();
        var dbStream = await _db.Streams.FirstOrDefaultAsync(x => x.CloudinaryStreamId == request.CloudinaryStreamId);
        if (dbStream is not null)
        {
            _db.Streams.Remove(dbStream);
            await _db.SaveChangesAsync();
        }

        return new Empty();
    }

    [Authorize]
    public override async Task<Empty> ActivateStream(StreamActionRequest request, ServerCallContext context)
    {
        await GetAuthorOrThrowIfBadAsync(request.CloudinaryStreamId);
        var httpRequest = new HttpRequestMessage(HttpMethod.Post,
            $"{BaseUrl}/live_streams/{request.CloudinaryStreamId}/activate");
        var response = await _httpClient.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();
        return new Empty();
    }

    [Authorize]
    public override async Task<Empty> IdleStream(StreamActionRequest request, ServerCallContext context)
    {
        await GetAuthorOrThrowIfBadAsync(request.CloudinaryStreamId);
        var httpRequest =
            new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/live_streams/{request.CloudinaryStreamId}/idle");
        var response = await _httpClient.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();
        return new Empty();
    }

    [Authorize]
    public override async Task<ListStreamOutputsReply> ListStreamOutputs(ListStreamOutputsRequest request,
        ServerCallContext context)
    {
        await GetAuthorOrThrowIfBadAsync(request.CloudinaryStreamId);
        var httpRequest = new HttpRequestMessage(HttpMethod.Get,
            $"{BaseUrl}/live_streams/{request.CloudinaryStreamId}/outputs");
        var response = await _httpClient.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();
        var listReply = new ListStreamOutputsReply();
        var resultJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(resultJson);
        var arr = doc.RootElement.GetProperty("data");
        foreach (var outputElem in arr.EnumerateArray())
        {
            var outId = outputElem.GetProperty("id").GetString() ?? "";
            var outName = outputElem.GetProperty("name").GetString() ?? "";
            var outType = outputElem.GetProperty("type").GetString() ?? "";
            var outCreatedMs = outputElem.TryGetProperty("created_at", out var outCreatedProp)
                ? outCreatedProp.GetInt64()
                : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var outUpdatedMs = outputElem.TryGetProperty("updated_at", out var outUpdatedProp)
                ? outUpdatedProp.GetInt64()
                : outCreatedMs;
            var outCreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(outCreatedMs).UtcDateTime;
            var outUpdatedAt = DateTimeOffset.FromUnixTimeMilliseconds(outUpdatedMs).UtcDateTime;
            var info = new StreamOutputInfo
            {
                OutputId = outId,
                Name = outName,
                Type = outType,
                CreatedAt = Timestamp.FromDateTime(outCreatedAt),
                UpdatedAt = Timestamp.FromDateTime(outUpdatedAt)
            };
            listReply.Outputs.Add(info);
        }

        return listReply;
    }

    [Authorize]
    public override async Task<StreamOutputInfo> AddStreamOutput(AddStreamOutputRequest request,
        ServerCallContext context)
    {
        await GetAuthorOrThrowIfBadAsync(request.CloudinaryStreamId);
        var bodyObj = new
        {
            name = request.Name,
            type = request.Type,
            uri = request.HasUri ? request.Uri : null,
            stream_key = request.HasStreamKey ? request.StreamKey : null,
            vendor = request.HasVendor ? request.Vendor : null,
            public_id = request.HasPublicId ? request.PublicId : null
        };
        var httpRequest =
            new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/live_streams/{request.CloudinaryStreamId}/outputs")
            {
                Content = new StringContent(JsonSerializer.Serialize(bodyObj), Encoding.UTF8, "application/json")
            };
        var response = await _httpClient.SendAsync(httpRequest);
        var (id, name, type, _, _, data) = await ParseStreamData(response);
        var outputInfo = new StreamOutputInfo
        {
            OutputId = id,
            Name = name,
            Type = type
        };
        if (outputInfo is { Name: "default archive", Type: "archive" })
        {
            var dbStream =
                await _db.Streams.FirstOrDefaultAsync(x => x.CloudinaryStreamId == request.CloudinaryStreamId);
            if (dbStream != null && data.TryGetProperty("public_id", out var pIdProp))
            {
                dbStream.ArchivePublicId = pIdProp.GetString() ?? "";
                _db.Streams.Update(dbStream);
                await _db.SaveChangesAsync();
            }
        }

        return outputInfo;
    }

    [Authorize]
    public override async Task<StreamOutputInfo> UpdateStreamOutput(UpdateStreamOutputRequest request,
        ServerCallContext context)
    {
        await GetAuthorOrThrowIfBadAsync(request.CloudinaryStreamId);
        var bodyObj = new
        {
            name = request.HasName ? request.Name : null,
            uri = request.HasUri ? request.Uri : null,
            stream_key = request.HasStreamKey ? request.StreamKey : null,
            public_id = request.HasPublicId ? request.PublicId : null
        };
        var httpRequest = new HttpRequestMessage(
            HttpMethod.Patch,
            $"{BaseUrl}/live_streams/{request.CloudinaryStreamId}/outputs/{request.OutputId}"
        )
        {
            Content = new StringContent(JsonSerializer.Serialize(bodyObj), Encoding.UTF8, "application/json")
        };
        var response = await _httpClient.SendAsync(httpRequest);
        var (id, name, type, _, _, data) = await ParseStreamData(response);
        var outputInfo = new StreamOutputInfo
        {
            OutputId = id,
            Name = name,
            Type = type
        };
        if (outputInfo is { Name: "default archive", Type: "archive" })
        {
            var dbStream =
                await _db.Streams.FirstOrDefaultAsync(x => x.CloudinaryStreamId == request.CloudinaryStreamId);
            if (dbStream != null && data.TryGetProperty("public_id", out var pIdProp))
            {
                dbStream.ArchivePublicId = pIdProp.GetString() ?? "";
                _db.Streams.Update(dbStream);
                await _db.SaveChangesAsync();
            }
        }

        return outputInfo;
    }

    [Authorize]
    public override async Task<Empty> DeleteStreamOutput(DeleteStreamOutputRequest request, ServerCallContext context)
    {
        await GetAuthorOrThrowIfBadAsync(request.CloudinaryStreamId);
        var httpRequest = new HttpRequestMessage(
            HttpMethod.Delete,
            $"{BaseUrl}/live_streams/{request.CloudinaryStreamId}/outputs/{request.OutputId}"
        );
        var response = await _httpClient.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();
        return new Empty();
    }

    [Authorize]
    public override async Task<ArchiveStreamReply> ArchiveStream(ArchiveStreamRequest request, ServerCallContext context)
    {
        await GetAuthorOrThrowIfBadAsync(request.CloudinaryStreamId);
        VoD.ArchiveStreamReply vodArchiveResponse;
        
        var authHeader = context.RequestHeaders.FirstOrDefault(h => h.Key == "Authorization");
        var headers = new Metadata();
        
        if (authHeader != null)
        {
            headers.Add("Authorization", $"Bearer {authHeader.Value}");
        }
        
        try
        {
            vodArchiveResponse = await _videoServiceClient.ArchiveStreamAsync(new VoD.ArchiveStreamRequest
            {
                PublicStreamId = request.CloudinaryStreamId,
                Title = request.Title,
                Description = request.Description,
                Tags = { request.Tags }
            }, cancellationToken: context.CancellationToken);

            if (vodArchiveResponse is null) 
            {
                throw new RpcException(new Status(StatusCode.Internal, "Archiving process with the VoD service failed or returned no information."));
            }
        }
        catch (RpcException) 
        {
            throw; 
        }
        catch (Exception ex) 
        {
            throw new RpcException(new Status(StatusCode.Internal, $"An unexpected error occurred while communicating with the video archiving service: {ex.Message}"));
        }

        var cloudinaryDeleteHttpRequest =
            new HttpRequestMessage(HttpMethod.Delete, $"{BaseUrl}/live_streams/{request.CloudinaryStreamId}");
        try
        {
            var cloudinaryHttpResponse = await _httpClient.SendAsync(cloudinaryDeleteHttpRequest, context.CancellationToken);
            if (!cloudinaryHttpResponse.IsSuccessStatusCode)
            {
                var errorContent = await cloudinaryHttpResponse.Content.ReadAsStringAsync(context.CancellationToken);
                throw new RpcException(new Status(StatusCode.Internal, $"Failed to delete live stream from Cloudinary. Status: {cloudinaryHttpResponse.StatusCode}. Response: {errorContent}"));
            }
        }
        catch (HttpRequestException ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, $"Error communicating with Cloudinary to delete live stream: {ex.Message}"));
        }

        var dbStream = await _db.Streams.FirstOrDefaultAsync(e => e.CloudinaryStreamId == request.CloudinaryStreamId, context.CancellationToken);
        if (dbStream is not null)
        {
            _db.Streams.Remove(dbStream);
            await _db.SaveChangesAsync(context.CancellationToken);
        }

        var reply = new ArchiveStreamReply
        {
            CloudinaryPublicId = vodArchiveResponse.PublicId
        };

        return reply;
    }

    public override async Task<GetStreamInfoReply> GetStreamInfo(GetStreamInfoRequest request,
        ServerCallContext context)
    {
        var httpRequest =
            new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/live_streams/{request.CloudinaryStreamId}");
        var response = await _httpClient.SendAsync(httpRequest);
        var authorId = await GetAuthorIdAsync(request.CloudinaryStreamId)
                       ?? throw new RpcException(new Status(StatusCode.NotFound, "Stream not found"));
        var (id, name, status, createdAt, _, data) = await ParseStreamData(response);
        var hlsUri = "";
        if (data.TryGetProperty("outputs", out var outputsElement) && outputsElement.ValueKind == JsonValueKind.Array)
            foreach (var output in outputsElement.EnumerateArray())
            {
                var outName = output.GetProperty("name").GetString() ?? "";
                var outType = output.GetProperty("type").GetString() ?? "";
                if (outName == "default hls" && outType == "hls")
                {
                    hlsUri = output.GetProperty("uri").GetString() ?? "";
                    break;
                }
            }

        var reply = new GetStreamInfoReply
        {
            Stream = new Streaming.StreamInfo
            {
                CloudinaryStreamId = id,
                Name = name,
                AuthorId = authorId,
                Status = status,
                CreatedAt = Timestamp.FromDateTime(createdAt),
                HlsPlaybackUri = hlsUri
            }
        };
        return reply;
    }

    public override async Task<ListStreamsReply> ListStreams(ListStreamsRequest request, ServerCallContext context)
    {
        var pageSize = request.PageSize > 0 ? request.PageSize : 10;
        int.TryParse(request.PageToken, out var pageToken);
        var skipCount = pageSize * pageToken;
        IQueryable<StreamInfo> query = _db.Streams;
        if (request.HasFilterAuthorId) query = query.Where(e => e.AuthorId == request.FilterAuthorId);
        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        var pageData = await query
            .OrderBy(x => x.Name)
            .Skip(skipCount)
            .Take(pageSize)
            .ToListAsync();
        var streamInfoTasks = new List<Task<HttpResponseMessage>>();
        foreach (var streamInfo in pageData)
        {
            var httpRequest =
                new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/live_streams/{streamInfo.CloudinaryStreamId}");
            streamInfoTasks.Add(_httpClient.SendAsync(httpRequest));
        }

        try
        {
            await Task.WhenAll(streamInfoTasks);
        }
        catch (AggregateException)
        {
            throw new RpcException(new Status(StatusCode.Internal, "Internal error while fetching stream data"));
        }

        var reply = new ListStreamsReply
        {
            PagesNumber = totalPages.ToString()
        };
        for (var i = 0; i < pageData.Count; i++)
        {
            var response = streamInfoTasks[i].Result;
            if (!response.IsSuccessStatusCode) continue;
            var resultJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(resultJson);
            if (doc.RootElement.TryGetProperty("data", out var dataElement))
            {
                var id = dataElement.GetProperty("id").GetString() ?? "";
                var name = dataElement.GetProperty("name").GetString() ?? "";
                var status = dataElement.GetProperty("status").GetString() ?? "";
                var createdMs = dataElement.TryGetProperty("created_at", out var createdAtProp)
                    ? createdAtProp.GetInt64()
                    : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var createdTime = DateTimeOffset.FromUnixTimeMilliseconds(createdMs).UtcDateTime;
                var hlsUri = "";
                if (dataElement.TryGetProperty("outputs", out var outputsElement) &&
                    outputsElement.ValueKind == JsonValueKind.Array)
                    foreach (var outElem in outputsElement.EnumerateArray())
                    {
                        var outName = outElem.GetProperty("name").GetString() ?? "";
                        var outType = outElem.GetProperty("type").GetString() ?? "";
                        if (outName == "default hls" && outType == "hls")
                        {
                            hlsUri = outElem.GetProperty("uri").GetString() ?? "";
                            break;
                        }
                    }

                reply.Streams.Add(new Streaming.StreamInfo
                {
                    CloudinaryStreamId = id,
                    Name = name,
                    AuthorId = pageData[i].AuthorId,
                    Status = status,
                    CreatedAt = Timestamp.FromDateTime(createdTime),
                    HlsPlaybackUri = hlsUri
                });
            }
        }

        return reply;
    }

    private async Task<int> GetAuthorOrThrowIfBadAsync(string streamId)
    {
        var info = await _db.Streams.FirstOrDefaultAsync(e => e.CloudinaryStreamId == streamId);
        if (info is null) throw new RpcException(new Status(StatusCode.NotFound, "Stream not found"));

        var httpContext = _httpContextAccessor.HttpContext;
        var userId = int.Parse(httpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                               throw new RpcException(new Status(StatusCode.Unauthenticated, "Unauthenticated")));
        var role = httpContext.User.FindFirst(ClaimTypes.Role)?.Value ??
                   throw new RpcException(new Status(StatusCode.Unauthenticated, "Unauthenticated"));

        if (role is "Admin" or "InnerService") return info.AuthorId;

        if (userId != info.AuthorId)
            throw new RpcException(new Status(StatusCode.PermissionDenied,
                "You do not have permission to manage this stream"));

        return info.AuthorId;
    }

    private async
        Task<(string id, string name, string status, DateTime createdAt, DateTime updatedAt, JsonElement data)>
        ParseStreamData(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        var resultJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(resultJson);
        var data = doc.RootElement.GetProperty("data");
        var id = data.GetProperty("id").GetString() ?? "";
        var name = data.GetProperty("name").GetString() ?? "";
        var status = data.GetProperty("status").GetString() ?? "";
        var createdMs = data.TryGetProperty("created_at", out var createdAtProp)
            ? createdAtProp.GetInt64()
            : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var updatedMs = data.TryGetProperty("updated_at", out var updatedAtProp)
            ? updatedAtProp.GetInt64()
            : createdMs;
        var createdAt = DateTimeOffset.FromUnixTimeMilliseconds(createdMs).UtcDateTime;
        var updatedAt = DateTimeOffset.FromUnixTimeMilliseconds(updatedMs).UtcDateTime;
        return (id, name, status, createdAt, updatedAt, data);
    }

    private static string ExtractArchivePublicId(JsonElement data)
    {
        var archivePublicId = "";
        if (data.TryGetProperty("outputs", out var outputsElement) && outputsElement.ValueKind == JsonValueKind.Array)
            foreach (var output in outputsElement.EnumerateArray())
            {
                var outName = output.GetProperty("name").GetString() ?? "";
                var outType = output.GetProperty("type").GetString() ?? "";
                if (outName == "default archive" && outType == "archive")
                {
                    archivePublicId = output.GetProperty("public_id").GetString() ?? "";
                    break;
                }
            }

        return archivePublicId;
    }

    private async Task<int?> GetAuthorIdAsync(string streamId)
    {
        var info = await _db.Streams.FirstOrDefaultAsync(e => e.CloudinaryStreamId == streamId);
        return info?.AuthorId ?? null;
    }

    private int GetUserId()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var userId = int.Parse(httpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                               throw new RpcException(new Status(StatusCode.Unauthenticated, "Unauthenticated")));
        return userId;
    }
}