FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 5000

ENV ASPNETCORE_ENVIRONMENT=Development
ENV JwtSettings__Key=""
ENV JwtSettings__Issuer=""
ENV JwtSettings__Audience=""
ENV ServiceAddresses__AuthService__Host=""
ENV ServiceAddresses__AuthService__Port=""
ENV ServiceAddresses__VodService__Host=""
ENV ServiceAddresses__VodService__Port=""
ENV ServiceAddresses__AccService__Host=""
ENV ServiceAddresses__AccService__Port=""
ENV ServiceAddresses__LiveService__Host=""
ENV ServiceAddresses__LiveService__Port=""


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