using System.IdentityModel.Tokens.Jwt;
using System.Net.Mail;
using System.Security.Authentication;
using System.Security.Claims;
using System.Security.Cryptography;
using Auth;
using AuthService.Models;
using AuthService.Persistence;
using AuthService.Service.HelpersImplementations;
using AuthService.Service.HelpersInterfaces;
using AuthService.Settings;
using Email;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Options;

namespace AuthService.Service;

public class AuthService(
    ILogger<JwtService> logger,
    IHashService hashService,
    IRepository<User> usersRepository,
    IRepository<UserRegRequest> requestsRepository,
    IRepository<RefreshToken> refreshTokensRepository,
    IJwtService jwtService,
    IOptions<AuthSettings> authOptions,
    IOptions<EncryptionSettings> encryptionOptions,
    AppDbContext appDbContext,
    ServiceAddresses addresses
) : Auth.AuthService.AuthServiceBase
{
    public override async Task<LoginReply> Login(LoginRequest request, ServerCallContext context)
    {
        try
        {
            var (jwtToken, refreshToken) =
                await jwtService.GenerateTokenAsync(request.Email, request.Password);
            return ConstructReply(jwtToken, refreshToken);
        }
        catch (AuthenticationException ex)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, ex.Message));
        }
        catch (ArgumentException ex)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, ex.Message));
        }
        catch (Exception ex)
        {
            logger.LogCritical("Inner exception in Login: {ex}", ex.Message);
            throw new RpcException(new Status(StatusCode.Internal, "No info"));
        }
    }

    public override async Task<Empty> BeginRegistration(RegisterRequest request, ServerCallContext context)
    {
        if (string.IsNullOrEmpty(request.Email) ||
            request.Password.Length < authOptions.Value.MinPasswordSize ||
            request.Password.Length > authOptions.Value.PasswordSize ||
            request.Email.Length > authOptions.Value.EmailSize)
            throw new RpcException(
                new Status(
                    StatusCode.InvalidArgument,
                    $"Password should be from {authOptions.Value.MinPasswordSize} to {authOptions.Value.PasswordSize} characters. " +
                    $"Email can't be empty and should be less than {authOptions.Value.EmailSize}."));

        if (!MailAddress.TryCreate(request.Email, out _))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid email."));

        var codeBytes = new byte[authOptions.Value.RegistrationCodeSize];
        var saltBytes = new byte[encryptionOptions.Value.SaltSize];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(codeBytes);
            rng.GetBytes(saltBytes);
        }

        var code = Convert.ToBase64String(codeBytes);
        var salt = Convert.ToBase64String(saltBytes);
        var passwordHash = await hashService.HashAsync(request.Password, salt);

        var userRegRequest = new UserRegRequest
        {
            Email = request.Email,
            PasswordHash = passwordHash,
            RegistrationCode = code,
            ActiveUntil = DateTime.UtcNow.AddMinutes(authOptions.Value.RegistrationCodeLifetimeMinutes)
        };

        await requestsRepository.AddAsync(userRegRequest, context.CancellationToken);
        await usersRepository.SaveChangesAsync(context.CancellationToken);

        var channel = GrpcChannel.ForAddress(addresses.EmailService);
        var client = new EmailService.EmailServiceClient(channel);
        await client.SendEmailAsync(new EmailRequest
        {
            From = "Registration",
            To = request.Email,
            Subject = "Registration code",
            Body = $"Your registration code: {code}"
        });

        return new Empty();
    }

    public override async Task<Empty> FinishRegistration(FinishRequest request, ServerCallContext context)
    {
        if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Code))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Email and code are required."));

        var regRequest = await requestsRepository.FirstOrDefaultAsync(
                             x => x.Email == request.Email && x.ActiveUntil > DateTime.UtcNow,
                             context.CancellationToken)
                         ?? throw new RpcException(new Status(StatusCode.NotFound,
                             "No valid registration request found for this email."));

        if (regRequest.RegistrationCode != request.Code)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid registration code."));

        var existingUser = await usersRepository.FirstOrDefaultAsync(
            x => x.Email == request.Email, context.CancellationToken);

        if (existingUser is not null)
            throw new RpcException(new Status(StatusCode.AlreadyExists, "User with this email already exists."));

        var user = new User
        {
            Email = regRequest.Email,
            PasswordHash = regRequest.PasswordHash
        };

        await usersRepository.AddAsync(user, context.CancellationToken);
        await requestsRepository.DeleteAsync(regRequest, context.CancellationToken);
        await usersRepository.SaveChangesAsync(context.CancellationToken);
        await requestsRepository.SaveChangesAsync(context.CancellationToken);

        return new Empty();
    }

    public override async Task<LoginReply> Refresh(RefreshRequest request, ServerCallContext context)
    {
        try
        {
            var (jwtToken, refreshToken) =
                await jwtService.RefreshTokenAsync(request.JwtToken, request.RefreshToken);
            return ConstructReply(jwtToken, refreshToken);
        }
        catch (AuthenticationException ex)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }
        catch (ArgumentException ex)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, ex.Message));
        }
        catch (Exception ex)
        {
            logger.LogCritical("Inner exception in Login: {ex}", ex.Message);
            throw new RpcException(new Status(StatusCode.Internal, "No info"));
        }
    }

    public override async Task<Empty> Logout(LogoutRequest request, ServerCallContext context)
    {
        if (string.IsNullOrEmpty(request.JwtToken))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "JWT token required"));

        var tokenHandler = new JwtSecurityTokenHandler();
        try
        {
            var jwtToken = tokenHandler.ReadJwtToken(request.JwtToken);
            var email = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;
            if (string.IsNullOrEmpty(email))
                throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid token (no email)."));

            var user = await usersRepository.FirstOrDefaultAsync(u => u.Email == email, context.CancellationToken)
                       ?? throw new RpcException(new Status(StatusCode.NotFound, "User not found"));

            var tokens = await refreshTokensRepository
                .ListAsync(r => r.UserId == user.Id, context.CancellationToken);

            foreach (var token in tokens)
                await refreshTokensRepository.DeleteAsync(token, context.CancellationToken);

            await refreshTokensRepository.SaveChangesAsync(context.CancellationToken);

            return new Empty();
        }
        catch (Exception)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid JWT token"));
        }
    }

    private LoginReply ConstructReply(string jwtToken, string refreshToken)
    {
        var expiresJwt = DateTime.UtcNow.Add(authOptions.Value.AccessTokenLifetime);
        var expiresRefresh = DateTime.UtcNow.Add(authOptions.Value.RefreshTokenLifetime);
        return new LoginReply
        {
            JwtToken = jwtToken,
            RefreshToken = refreshToken,
            ExpiresJwt = expiresJwt.ToTimestamp(),
            ExpiresRefresh = expiresRefresh.ToTimestamp()
        };
    }
}