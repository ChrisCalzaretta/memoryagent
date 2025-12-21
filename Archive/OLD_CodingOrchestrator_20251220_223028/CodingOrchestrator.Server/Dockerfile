FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5003

# 🐳 Install Docker CLI for code execution in containers
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

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["CodingOrchestrator.Server/CodingOrchestrator.Server.csproj", "CodingOrchestrator.Server/"]
COPY ["Shared/AgentContracts/AgentContracts.csproj", "Shared/AgentContracts/"]
RUN dotnet restore "CodingOrchestrator.Server/CodingOrchestrator.Server.csproj"
COPY . .
WORKDIR "/src/CodingOrchestrator.Server"
RUN dotnet build "CodingOrchestrator.Server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CodingOrchestrator.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://+:5003
ENTRYPOINT ["dotnet", "CodingOrchestrator.Server.dll"]


