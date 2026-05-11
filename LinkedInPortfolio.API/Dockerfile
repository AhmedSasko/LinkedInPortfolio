FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY LinkedInPortfolio.API/LinkedInPortfolio.API.csproj LinkedInPortfolio.API/
RUN dotnet restore LinkedInPortfolio.API/LinkedInPortfolio.API.csproj

COPY LinkedInPortfolio.API/ LinkedInPortfolio.API/
WORKDIR /src/LinkedInPortfolio.API
RUN dotnet publish -c Release -o /app/publish

# Debian Bookworm slim — has a real chromium deb (no snap), works on amd64 + arm64
FROM mcr.microsoft.com/dotnet/aspnet:10.0-bookworm-slim AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    chromium \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
# Tell PuppeteerSharp to use the system Chromium; ChromiumDownloaderService skips download when set
ENV PUPPETEER_EXECUTABLE_PATH=/usr/bin/chromium
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "LinkedInPortfolio.API.dll"]
