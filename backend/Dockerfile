# syntax=docker/dockerfile:1

# Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj first for better layer caching
COPY backend/BankReporting.Api.csproj backend/
RUN dotnet restore backend/BankReporting.Api.csproj

# Copy the rest and publish
COPY . .
RUN dotnet publish backend/BankReporting.Api.csproj -c Release -o /app/publish

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "BankReporting.Api.dll"]
