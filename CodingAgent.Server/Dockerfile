FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5001

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
ENV ASPNETCORE_URLS=http://+:5001
ENTRYPOINT ["dotnet", "CodingAgent.Server.dll"]


