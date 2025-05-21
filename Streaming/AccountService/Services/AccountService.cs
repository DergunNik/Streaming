using Account;
using AccountService.Data;
using AccountService.Models;
using AccountService.Settings;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;

namespace AccountService.Services;

public class AccountService : Account.AccountService.AccountServiceBase
{
    private readonly AppDbContext _dbContext;
    private readonly Cloudinary _cloudinary;
    private readonly ContentRestrictions _restrictions;

    public AccountService(AppDbContext dbContext, Cloudinary cloudinary, ContentRestrictions restrictions)
    {
        _dbContext = dbContext;
        _cloudinary = cloudinary;
        _restrictions = restrictions;
    }

    public override async Task<GetAccountReply> GetAccount(GetAccountRequest request, ServerCallContext context)
    {
        var user = await _dbContext.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.UserId == request.UserId, context.CancellationToken);

        if (user is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"User {request.UserId} not found"));
        }

        return new GetAccountReply
        {
            Info = MapToProto(user, banMask: user.IsBanned)
        };
    }

    public override async Task<ListAccountsReply> ListAccounts(ListAccountsRequest request, ServerCallContext context)
    {
        var query = _dbContext.Accounts.AsNoTracking();

        if (request.HasIsBannedStatus)
        {
            query = query.Where(a => a.IsBanned == request.IsBannedStatus);
        }

        var totalCount = await query.CountAsync(context.CancellationToken);

        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Max(request.PageSize, 1);

        var accounts = await query
            .OrderBy(a => a.UserId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(context.CancellationToken);

        var reply = new ListAccountsReply { TotalCount = totalCount };
        reply.Accounts.AddRange(accounts.Select(a => MapToProto(a, banMask: a.IsBanned)));
        return reply;
    }

    public override async Task<CreateAccountReply> CreateAccount(CreateAccountRequest request, ServerCallContext context)
    {
        var info = request.Info;
        if (await _dbContext.Accounts.AnyAsync(a => a.UserId == info.UserId, context.CancellationToken))
        {
            throw new RpcException(new Status(StatusCode.AlreadyExists, "Account already exists"));
        }

        var (avatarPublicId, backgroundPublicId, description) = await ProcessAndValidateAccountInfo(info);

        var entity = new Models.AccountInfo
        {
            UserId = info.UserId,
            AvatarPublicId = avatarPublicId,
            BackgroundPublicId = backgroundPublicId,
            Description = description
        };

        _dbContext.Accounts.Add(entity);
        await _dbContext.SaveChangesAsync(context.CancellationToken);

        return new CreateAccountReply
        {
            Info = MapToProto(entity, banMask: entity.IsBanned)
        };
    }

    public override async Task<UpdateAccountReply> UpdateAccount(UpdateAccountRequest request, ServerCallContext context)
    {
        var info = request.Info;
        var userId = info.UserId;

        var account = await _dbContext.Accounts.FirstOrDefaultAsync(a => a.UserId == userId, context.CancellationToken);
        if (account is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"User {info.UserId} not found"));
        }

        if (account.IsBanned)
        {
            return new UpdateAccountReply
            {
                Info = MapToProto(account, true)
            };
        }

        var (avatarPublicId, backgroundPublicId, description) = await ProcessAndValidateAccountInfo(info, forUpdate: true);

        account.Description = description ?? account.Description;
        account.AvatarPublicId = avatarPublicId ?? account.AvatarPublicId;
        account.BackgroundPublicId = backgroundPublicId ?? account.BackgroundPublicId;

        await _dbContext.SaveChangesAsync(context.CancellationToken);

        return new UpdateAccountReply
        {
            Info = MapToProto(account, banMask: account.IsBanned)
        };
    }

    public override async Task<Empty> SetBanStatus(AccountBanRequest request, ServerCallContext context)
    {
        var account = await _dbContext.Accounts.FirstOrDefaultAsync(a => a.UserId == request.UserId, context.CancellationToken);

        if (account is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"User {request.UserId} not found"));
        }

        account.IsBanned = request.IsBanned;
        await _dbContext.SaveChangesAsync(context.CancellationToken);
        return new Empty();
    }

    private static Account.AccountInfo MapToProto(Models.AccountInfo entity, bool banMask)
    {
        var proto = new Account.AccountInfo
        {
            UserId = entity.UserId,
            IsBanned = entity.IsBanned
        };
        if (banMask)
        {
            return proto;
        }
        if (entity.AvatarPublicId is not null && entity.AvatarPublicId != string.Empty)
        {
            proto.AvatarPublicId = entity.AvatarPublicId;
        }
        if (entity.BackgroundPublicId is not null && entity.BackgroundPublicId != string.Empty)
        {
            proto.BackgroundPublicId = entity.BackgroundPublicId;
        }
        if (entity.Description is not null && entity.Description != string.Empty)
        {
            proto.Description = entity.Description;
        }
        return proto;
    }

    private async Task<(string? avatarPublicId, string? backgroundPublicId, string? description)> ProcessAndValidateAccountInfo(SetAccountInfo info, bool forUpdate = false)
    {
        string? avatarPublicId = null;
        string? backgroundPublicId = null;
        string? description = null;

        if (info.HasDescription)
        {
            if (info.Description.Length > _restrictions.MaxDescriptionLength)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, $"Description too long (max {_restrictions.MaxDescriptionLength})"));
            }
            description = info.Description;
        }
        else if (!forUpdate)
        {
            description = string.Empty;
        }

        if (info.HasAvatarImage)
        {
            if (info.AvatarImage.Length > _restrictions.MaxImageSizeBytes)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, $"Avatar image too large (max {_restrictions.MaxImageSizeBytes} bytes)"));
            }

            CheckAspectRatio(info.AvatarImage.ToByteArray(), _restrictions.AvatarAspectRatioMin, _restrictions.AvatarAspectRatioMax, "avatar");
            avatarPublicId = await UploadImageToCloudinary(info.AvatarImage.ToByteArray(), "avatars");
        }

        if (info.HasBackgroundImage)
        {
            if (info.BackgroundImage.Length > _restrictions.MaxImageSizeBytes)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, $"Background image too large (max {_restrictions.MaxImageSizeBytes} bytes)"));
            }

            CheckAspectRatio(info.BackgroundImage.ToByteArray(), _restrictions.BackgroundAspectRatioMin, _restrictions.BackgroundAspectRatioMax, "background");
            backgroundPublicId = await UploadImageToCloudinary(info.BackgroundImage.ToByteArray(), "backgrounds");
        }

        return (avatarPublicId, backgroundPublicId, description);
    }

    private async Task<string> UploadImageToCloudinary(byte[] image, string folder)
    {
        using var stream = new MemoryStream(image);
        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription("image", stream),
            Folder = folder,
            Invalidate = true
        };
        var result = await _cloudinary.UploadAsync(uploadParams);
        if (result.Error is not null)
        {
            throw new RpcException(new Status(StatusCode.Internal, result.Error.Message));
        }
        return result.PublicId;
    }

    private void CheckAspectRatio(byte[] image, double min, double max, string type)
    {
        const int error = (int)1e-6;
        using var img = SixLabors.ImageSharp.Image.Load(image);
        var ratio = (double)img.Width / img.Height;
        if (ratio < min - error || ratio > max + error)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"{type} image must have aspect ratio {min}:1"));
        }
    }
}