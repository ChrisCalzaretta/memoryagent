# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj and restore
COPY MemoryAgent.Server/MemoryAgent.Server.csproj MemoryAgent.Server/
RUN dotnet restore "MemoryAgent.Server/MemoryAgent.Server.csproj"

# Copy everything else and build
COPY MemoryAgent.Server/ MemoryAgent.Server/
WORKDIR "/src/MemoryAgent.Server"
RUN dotnet build "MemoryAgent.Server.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "MemoryAgent.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 5000

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MemoryAgent.Server.dll"]

