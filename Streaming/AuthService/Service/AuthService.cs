using System.IdentityModel.Tokens.Jwt;
using System.Net.Mail;
using System.Security.Authentication;
using System.Security.Claims;
using System.Security.Cryptography;
using AuthServerApp;
using AuthService.Data;
using AuthService.Models;
using AuthService.Service.HelpersImplementations;
using AuthService.Service.HelpersInterfaces;
using AuthService.Settings;
using EmailClientApp;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Options;

namespace AuthService.Service;

public class AuthService : AuthServerApp.AuthService.AuthServiceBase
{
    private readonly ILogger<JwtService> _logger;
    private readonly IHashService _hashService;
    private readonly IRepository<User> _usersRepository;
    private readonly IRepository<UserRegRequest> _requestsRepository;
    private readonly IRepository<RefreshToken> _refreshTokensRepository;
    private readonly IJwtService _jwtService;
    private readonly IOptions<AuthSettings> _authOptions;
    private readonly IOptions<EncryptionSettings> _encryptionOptions;
    private readonly EmailService.EmailServiceClient _emailServiceClient;

    public AuthService(
        ILogger<JwtService> logger,
        IHashService hashService,
        IRepository<User> usersRepository,
        IRepository<UserRegRequest> requestsRepository,
        IRepository<RefreshToken> refreshTokensRepository,
        IJwtService jwtService,
        IOptions<AuthSettings> authOptions,
        IOptions<EncryptionSettings> encryptionOptions,
        EmailService.EmailServiceClient emailServiceClient)
    {
        _logger = logger;
        _hashService = hashService;
        _usersRepository = usersRepository;
        _requestsRepository = requestsRepository;
        _refreshTokensRepository = refreshTokensRepository;
        _jwtService = jwtService;
        _authOptions = authOptions;
        _encryptionOptions = encryptionOptions;
        _emailServiceClient = emailServiceClient;
    }
    
    public override async Task<LoginReply> Login(LoginRequest request, ServerCallContext context)
    {
        try
        {
            var (jwtToken, refreshToken) =
                await _jwtService.GenerateTokenAsync(request.Email, request.Password);
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
            _logger.LogCritical("Inner exception in Login: {ex}", ex.Message);
            throw new RpcException(new Status(StatusCode.Internal, "No info"));
        }
    }

    public override async Task<Empty> BeginRegistration(RegisterRequest request, ServerCallContext context)
    {
        if (string.IsNullOrEmpty(request.Email) ||
            request.Password.Length < _authOptions.Value.MinPasswordSize ||
            request.Password.Length > _authOptions.Value.PasswordSize ||
            request.Email.Length > _authOptions.Value.EmailSize)
            throw new RpcException(
                new Status(
                    StatusCode.InvalidArgument,
                    $"Password should be from {_authOptions.Value.MinPasswordSize} to {_authOptions.Value.PasswordSize} characters. " +
                    $"Email can't be empty and should be less than {_authOptions.Value.EmailSize}."));

        if (!MailAddress.TryCreate(request.Email, out _))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid email."));

        var codeBytes = new byte[_authOptions.Value.RegistrationCodeSize];
        var saltBytes = new byte[_encryptionOptions.Value.SaltSize];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(codeBytes);
            rng.GetBytes(saltBytes);
        }

        var code = Convert.ToBase64String(codeBytes);
        var salt = Convert.ToBase64String(saltBytes);
        var passwordHash = await _hashService.HashAsync(request.Password, salt);

        var userRegRequest = new UserRegRequest
        {
            Email = request.Email,
            PasswordHash = passwordHash,
            RegistrationCode = code,
            ActiveUntil = DateTime.UtcNow.AddMinutes(_authOptions.Value.RegistrationCodeLifetimeMinutes)
        };
    
        await _requestsRepository.AddAsync(userRegRequest, context.CancellationToken);
        await _usersRepository.SaveChangesAsync(context.CancellationToken);
        
        await _emailServiceClient.SendEmailAsync(new EmailRequest
        {
            From = "Registration",
            To = { request.Email },
            Subject = "Registration code",
            Body = $"Your registration code: {code}"
        },
        cancellationToken: context.CancellationToken);
        
        return new Empty();
    }

    public override async Task<Empty> FinishRegistration(FinishRequest request, ServerCallContext context)
    {
        if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Code))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Email and code are required."));

        var regRequest = await _requestsRepository.FirstOrDefaultAsync(
                             x => x.Email == request.Email && x.ActiveUntil > DateTime.UtcNow,
                             context.CancellationToken)
                         ?? throw new RpcException(new Status(StatusCode.NotFound,
                             "No valid registration request found for this email."));

        if (regRequest.RegistrationCode != request.Code)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid registration code."));

        var existingUser = await _usersRepository.FirstOrDefaultAsync(
            x => x.Email == request.Email, context.CancellationToken);

        if (existingUser is not null)
            throw new RpcException(new Status(StatusCode.AlreadyExists, "User with this email already exists."));

        var user = new User
        {
            Email = regRequest.Email,
            PasswordHash = regRequest.PasswordHash
        };

        await _usersRepository.AddAsync(user, context.CancellationToken);
        await _requestsRepository.DeleteAsync(regRequest, context.CancellationToken);
        await _usersRepository.SaveChangesAsync(context.CancellationToken);
        await _requestsRepository.SaveChangesAsync(context.CancellationToken);

        return new Empty();
    }

    public override async Task<LoginReply> Refresh(RefreshRequest request, ServerCallContext context)
    {
        try
        {
            var (jwtToken, refreshToken) =
                await _jwtService.RefreshTokenAsync(request.JwtToken, request.RefreshToken);
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
            _logger.LogCritical("Inner exception in Login: {ex}", ex.Message);
            throw new RpcException(new Status(StatusCode.Internal, "No info"));
        }
    }
    
    public override async Task<Empty> Logout(LogoutRequest request, ServerCallContext context)
    {
        if (string.IsNullOrEmpty(request.Email))
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Email required"));

        var user = await _usersRepository.FirstOrDefaultAsync(u => u.Email == request.Email, context.CancellationToken)
                   ?? throw new RpcException(new Status(StatusCode.NotFound, "User not found"));

        var tokens = await _refreshTokensRepository
            .ListAsync(r => r.UserId == user.Id, context.CancellationToken);

        foreach (var token in tokens)
            await _refreshTokensRepository.DeleteAsync(token, context.CancellationToken);

        await _refreshTokensRepository.SaveChangesAsync(context.CancellationToken);

        return new Empty();
    }

    private LoginReply ConstructReply(string jwtToken, string refreshToken)
    {
        var expiresJwt = DateTime.UtcNow.Add(_authOptions.Value.AccessTokenLifetime);
        var expiresRefresh = DateTime.UtcNow.Add(_authOptions.Value.RefreshTokenLifetime);
        return new LoginReply
        {
            JwtToken = jwtToken,
            RefreshToken = refreshToken,
            ExpiresJwt = expiresJwt.ToTimestamp(),
            ExpiresRefresh = expiresRefresh.ToTimestamp()
        };
    }
}