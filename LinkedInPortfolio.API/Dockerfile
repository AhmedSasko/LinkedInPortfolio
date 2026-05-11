FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY LinkedInPortfolio.API/LinkedInPortfolio.API.csproj LinkedInPortfolio.API/
RUN dotnet restore LinkedInPortfolio.API/LinkedInPortfolio.API.csproj

COPY LinkedInPortfolio.API/ LinkedInPortfolio.API/
WORKDIR /src/LinkedInPortfolio.API
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "LinkedInPortfolio.API.dll"]
