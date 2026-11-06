# Stage 1: build. Has the full SDK (compiler, NuGet); none of it reaches the final image.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore before copying the source, so a code change doesn't re-download packages:
# Docker reuses this cached layer until the .csproj itself changes.
COPY Homestay.Api/Homestay.Api.csproj Homestay.Api/
RUN dotnet restore Homestay.Api/Homestay.Api.csproj

COPY Homestay.Api/ Homestay.Api/
RUN dotnet publish Homestay.Api/Homestay.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# Builds the migrations bundle: one executable that applies migrations and seed data, then exits.
FROM build AS bundle
COPY dotnet-tools.json .
RUN dotnet tool restore
RUN dotnet ef migrations bundle --project Homestay.Api/Homestay.Api.csproj -o /app/efbundle --force

# A separate image holding just that bundle, used by docker compose and the deploy's migration job.
# Only built when asked for by name (--target migrator); it never ships with the API.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS migrator
WORKDIR /app
COPY --from=bundle /app/efbundle .
# The bundle unpacks itself on start, so it needs a folder this non-root user can write to.
ENV DOTNET_BUNDLE_EXTRACT_BASE_DIR=/tmp/.net
USER $APP_UID
ENTRYPOINT ["/app/efbundle"]

# Stage 2: runtime. Only the ASP.NET runtime and the published output.
# Last in the file, so a plain "docker build" produces this image.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# Non-root user built into the .NET images.
USER $APP_UID
ENTRYPOINT ["dotnet", "Homestay.Api.dll"]
