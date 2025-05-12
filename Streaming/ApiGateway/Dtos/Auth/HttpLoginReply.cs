namespace ApiGateway.Dtos.Auth;

public class HttpLoginReply
{
    public string JwtToken { get; set; }
    public string RefreshToken { get; set; }
    public DateTimeOffset ExpiresJwt { get; set; }
    public DateTimeOffset ExpiresRefresh { get; set; }
}