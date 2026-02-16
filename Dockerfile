# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["IIScribe.sln", "./"]
COPY ["src/IIScribe.Web/IIScribe.Web.csproj", "src/IIScribe.Web/"]
COPY ["src/IIScribe.Core/IIScribe.Core.csproj", "src/IIScribe.Core/"]
COPY ["src/IIScribe.Infrastructure/IIScribe.Infrastructure.csproj", "src/IIScribe.Infrastructure/"]
COPY ["src/IIScribe.CLI/IIScribe.CLI.csproj", "src/IIScribe.CLI/"]

# Restore dependencies
RUN dotnet restore "IIScribe.sln"

# Copy source code
COPY . .

# Build
WORKDIR "/src/src/IIScribe.Web"
RUN dotnet build "IIScribe.Web.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "IIScribe.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Create non-root user
RUN groupadd -r iiscribe && useradd -r -g iiscribe iiscribe

# Copy published app
COPY --from=publish /app/publish .

# Create directories for logs and data
RUN mkdir -p /app/logs /app/data && \
    chown -R iiscribe:iiscribe /app

# Switch to non-root user
USER iiscribe

# Expose ports
EXPOSE 8080
EXPOSE 8081

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true

ENTRYPOINT ["dotnet", "IIScribe.Web.dll"]
