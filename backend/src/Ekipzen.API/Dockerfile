# Build context is the `backend/` directory (see docker-compose.yml).
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore first, leveraging Docker layer caching for the project + CPM files.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY Ekipzen.sln ./
COPY src/Ekipzen.Domain/Ekipzen.Domain.csproj src/Ekipzen.Domain/
COPY src/Ekipzen.Application/Ekipzen.Application.csproj src/Ekipzen.Application/
COPY src/Ekipzen.Infrastructure/Ekipzen.Infrastructure.csproj src/Ekipzen.Infrastructure/
COPY src/Ekipzen.API/Ekipzen.API.csproj src/Ekipzen.API/
RUN dotnet restore src/Ekipzen.API/Ekipzen.API.csproj

# Copy the rest and publish.
COPY src/ src/
RUN dotnet publish src/Ekipzen.API/Ekipzen.API.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Ekipzen.API.dll"]
