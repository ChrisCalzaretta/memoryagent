FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
EXPOSE 5010

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files
COPY ["MemoryRouter.Server/MemoryRouter.Server.csproj", "MemoryRouter.Server/"]

# Restore dependencies
RUN dotnet restore "MemoryRouter.Server/MemoryRouter.Server.csproj"

# Copy source code
COPY MemoryRouter.Server/ MemoryRouter.Server/

# Build
WORKDIR "/src/MemoryRouter.Server"
RUN dotnet build "MemoryRouter.Server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MemoryRouter.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENV ASPNETCORE_URLS=http://+:5010

ENTRYPOINT ["dotnet", "MemoryRouter.Server.dll"]


