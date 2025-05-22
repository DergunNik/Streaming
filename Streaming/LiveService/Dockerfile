FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 5000

ENV ASPNETCORE_ENVIRONMENT=Development
ENV JwtSettings__Key=""
ENV JwtSettings__Issuer=""
ENV JwtSettings__Audience=""
ENV DbCredentials__User=""
ENV DbCredentials__Password=""
ENV DbCredentials__Db=""
ENV DbCredentials__Host="" 
ENV DbCredentials__Port=""
ENV ConnectionStrings__RedisSignalRBackplane=""
ENV CloudinarySettings__CloudName=""
ENV CloudinarySettings__ApiKey=""
ENV CloudinarySettings__ApiSecret=""

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["LiveService.csproj", "./"]
RUN dotnet restore "LiveService.csproj"
COPY . .
WORKDIR "/src"
RUN dotnet build "LiveService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "LiveService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "LiveService.dll"]
