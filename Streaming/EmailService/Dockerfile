FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 5000

ENV ASPNETCORE_ENVIRONMENT=Development
ENV EmailCredentials__Email=""
ENV EmailCredentials__AppPassword=""
ENV JwtSettings__Key=""
ENV JwtSettings__Issuer=""
ENV JwtSettings__Audience=""

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["EmailService.csproj", "./"]
RUN dotnet restore "EmailService.csproj"
COPY . .
WORKDIR "/src"
RUN dotnet build "EmailService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "EmailService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "EmailService.dll"]