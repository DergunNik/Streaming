FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base_app_runtime
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
ENV EmailServiceAddress__Host=""
ENV EmailServiceAddress__HttpPort=""
ENV EmailServiceAddress__GrpcPort=""

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS common_builder
WORKDIR /src
COPY ["AuthService.csproj", "./"]
COPY . .
RUN dotnet restore "AuthService.csproj"
RUN dotnet tool restore

FROM common_builder AS app_build_step
WORKDIR /src
RUN dotnet build "AuthService.csproj" -c Release -o /app/build

FROM app_build_step AS app_publish_step
RUN dotnet publish "AuthService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base_app_runtime AS app
WORKDIR /app
COPY --from=app_publish_step /app/publish .
ENTRYPOINT ["dotnet", "AuthService.dll"]

FROM common_builder AS migrator
WORKDIR /src

ENTRYPOINT ["dotnet", "ef", "database", "update", "--project", "AuthService.csproj"]