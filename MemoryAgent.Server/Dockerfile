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

# Install Python and Semgrep for security scanning
RUN apt-get update && apt-get install -y \
    python3 \
    python3-pip \
    python3-venv \
    && pip3 install --break-system-packages --no-cache-dir semgrep \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Verify Semgrep installation
RUN semgrep --version

WORKDIR /app
EXPOSE 5000

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MemoryAgent.Server.dll"]

