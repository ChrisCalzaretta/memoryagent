FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5002

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["ValidationAgent.Server/ValidationAgent.Server.csproj", "ValidationAgent.Server/"]
COPY ["Shared/AgentContracts/AgentContracts.csproj", "Shared/AgentContracts/"]
RUN dotnet restore "ValidationAgent.Server/ValidationAgent.Server.csproj"
COPY . .
WORKDIR "/src/ValidationAgent.Server"
RUN dotnet build "ValidationAgent.Server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ValidationAgent.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://+:5002
ENTRYPOINT ["dotnet", "ValidationAgent.Server.dll"]


