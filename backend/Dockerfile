FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY backend/BankReporting.Api.csproj backend/
RUN dotnet restore backend/BankReporting.Api.csproj

COPY backend/ backend/
WORKDIR /src/backend
RUN dotnet publish BankReporting.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000

ENTRYPOINT ["dotnet", "BankReporting.Api.dll"]
