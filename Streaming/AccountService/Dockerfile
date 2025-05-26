FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

ENV ASPNETCORE_ENVIRONMENT=Development
ENV DbCredentials__User=""
ENV DbCredentials__Password=""
ENV DbCredentials__Db=""
ENV DbCredentials__Host="" 
ENV DbCredentials__Port=""
ENV CloudinarySettings__CloudName=""
ENV CloudinarySettings__ApiKey=""
ENV CloudinarySettings__ApiSecret=""
ENV JwtSettings__Key=""
ENV JwtSettings__Issuer=""
ENV JwtSettings__Audience=""

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["AccountService.csproj", "./"]
RUN dotnet restore "AccountService.csproj"
COPY . .
WORKDIR "/src"
RUN dotnet build "AccountService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AccountService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AccountService.dll"]