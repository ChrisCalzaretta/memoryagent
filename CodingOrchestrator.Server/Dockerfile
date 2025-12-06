FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 5003

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


