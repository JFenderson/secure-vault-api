FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/SecureVaultAPI/SecureVaultAPI.csproj", "src/SecureVaultAPI/"]
RUN dotnet restore "src/SecureVaultAPI/SecureVaultAPI.csproj"
COPY . .
WORKDIR "/src/src/SecureVaultAPI"
RUN dotnet build "SecureVaultAPI.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "SecureVaultAPI.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "SecureVaultAPI.dll"]