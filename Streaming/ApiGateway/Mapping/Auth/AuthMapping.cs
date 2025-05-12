using ApiGateway.Dtos.Auth;
using AuthClientApp;
using Mapster;

namespace ApiGateway.Mapping.Auth;

public class AuthMapping : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<LoginReply, HttpLoginReply>()
            .Map(dest => dest.ExpiresJwt, src => src.ExpiresJwt.ToDateTimeOffset())
            .Map(dest => dest.ExpiresRefresh, src => src.ExpiresRefresh.ToDateTimeOffset());
    }
}