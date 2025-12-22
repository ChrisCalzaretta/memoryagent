FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5001

# 🐳 Install Docker CLI for dynamic code execution in containers
RUN apt-get update && apt-get install -y \
    ca-certificates \
    curl \
    gnupg \
    && install -m 0755 -d /etc/apt/keyrings \
    && curl -fsSL https://download.docker.com/linux/debian/gpg | gpg --dearmor -o /etc/apt/keyrings/docker.gpg \
    && chmod a+r /etc/apt/keyrings/docker.gpg \
    && echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/debian $(. /etc/os-release && echo "$VERSION_CODENAME") stable" | tee /etc/apt/sources.list.d/docker.list > /dev/null \
    && apt-get update \
    && apt-get install -y docker-ce-cli \
    && rm -rf /var/lib/apt/lists/*

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["CodingAgent.Server/CodingAgent.Server.csproj", "CodingAgent.Server/"]
COPY ["Shared/AgentContracts/AgentContracts.csproj", "Shared/AgentContracts/"]
RUN dotnet restore "CodingAgent.Server/CodingAgent.Server.csproj"
COPY . .
WORKDIR "/src/CodingAgent.Server"
RUN dotnet build "CodingAgent.Server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CodingAgent.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
# Explicitly copy wwwroot for static files
COPY --from=build /src/CodingAgent.Server/wwwroot ./wwwroot
ENV ASPNETCORE_URLS=http://+:5001
ENTRYPOINT ["dotnet", "CodingAgent.Server.dll"]


