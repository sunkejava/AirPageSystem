FROM node:24-alpine AS web
WORKDIR /src/web
COPY src/airpage-web/package*.json ./
RUN npm ci
COPY src/airpage-web/ ./
RUN npm run build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/AirPageSystem.Api/AirPageSystem.Api.csproj src/AirPageSystem.Api/
RUN dotnet restore src/AirPageSystem.Api/AirPageSystem.Api.csproj
COPY src/AirPageSystem.Api/ src/AirPageSystem.Api/
COPY --from=web /src/AirPageSystem.Api/wwwroot/ src/AirPageSystem.Api/wwwroot/
RUN dotnet publish src/AirPageSystem.Api/AirPageSystem.Api.csproj -c Release -o /app/publish --no-restore
FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update && apt-get install -y --no-install-recommends fonts-noto-cjk && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p /app/data
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
VOLUME ["/app/data"]
ENTRYPOINT ["dotnet","AirPageSystem.Api.dll"]
