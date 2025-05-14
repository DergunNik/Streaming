FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 5000

ENV ASPNETCORE_ENVIRONMENT=Development
ENV JwtSettings__Key=""
ENV ServiceAddresses__AuthService__Host=""
ENV ServiceAddresses__AuthService__HttpPort=""
ENV ServiceAddresses__AuthService__GrpcPort=""

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["ApiGateway.csproj", "./"]
RUN dotnet restore "ApiGateway.csproj"
COPY . .
WORKDIR "/src"
RUN dotnet build "ApiGateway.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ApiGateway.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ApiGateway.dll"]