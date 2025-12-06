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

# Install Python, Node.js, and Semgrep for code parsing and security scanning
RUN apt-get update && apt-get install -y \
    python3 \
    python3-pip \
    python3-venv \
    nodejs \
    npm \
    && pip3 install --break-system-packages --no-cache-dir semgrep \
    && npm install -g typescript @types/node \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Verify installations
RUN semgrep --version && python3 --version && node --version && tsc --version

WORKDIR /app
EXPOSE 5000

COPY --from=publish /app/publish .
# Copy TypeScript parser script (not included in .NET publish)
COPY MemoryAgent.Server/CodeAnalysis/ts-parser.js /app/CodeAnalysis/

# Ensure local TypeScript runtime dependency for the parser (global installs can be missing in runtime resolution)
RUN npm install --prefix /app/CodeAnalysis typescript
ENTRYPOINT ["dotnet", "MemoryAgent.Server.dll"]

