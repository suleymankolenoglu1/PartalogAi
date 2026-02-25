FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Katalogcu.sln ./
COPY Katalogcu.API/Katalogcu.API.csproj Katalogcu.API/
COPY Katalogcu.Application/Katalogcu.Application.csproj Katalogcu.Application/
COPY Katalogcu.Domain/Katalogcu.Domain.csproj Katalogcu.Domain/
COPY Katalogcu.Infrastructure/Katalogcu.Infrastructure.csproj Katalogcu.Infrastructure/
RUN dotnet restore Katalogcu.API/Katalogcu.API.csproj

COPY . .
RUN dotnet publish Katalogcu.API/Katalogcu.API.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Healthcheck ve genel tanılama için curl
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Development
EXPOSE 8080

ENTRYPOINT ["dotnet", "Katalogcu.API.dll"]
