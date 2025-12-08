FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5004

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files
COPY ["DesignAgent.Server/DesignAgent.Server.csproj", "DesignAgent.Server/"]
COPY ["Shared/AgentContracts/AgentContracts.csproj", "Shared/AgentContracts/"]

# Restore
RUN dotnet restore "DesignAgent.Server/DesignAgent.Server.csproj"

# Copy everything else
COPY . .

# Build
WORKDIR "/src/DesignAgent.Server"
RUN dotnet build "DesignAgent.Server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DesignAgent.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://+:5004
ENTRYPOINT ["dotnet", "DesignAgent.Server.dll"]

