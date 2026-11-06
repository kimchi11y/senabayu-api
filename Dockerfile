# Stage 1: build. Has the full SDK (compiler, NuGet); none of it reaches the final image.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore before copying the source, so a code change doesn't re-download packages:
# Docker reuses this cached layer until the .csproj itself changes.
COPY Homestay.Api/Homestay.Api.csproj Homestay.Api/
RUN dotnet restore Homestay.Api/Homestay.Api.csproj

COPY Homestay.Api/ Homestay.Api/
RUN dotnet publish Homestay.Api/Homestay.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# Stage 2: runtime. Only the ASP.NET runtime and the published output.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# Non-root user built into the .NET images.
USER $APP_UID
ENTRYPOINT ["dotnet", "Homestay.Api.dll"]
