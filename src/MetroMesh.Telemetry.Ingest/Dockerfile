FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY MetroMesh.slnx ./
COPY src/ ./src/
RUN dotnet publish src/MetroMesh.Telemetry.Ingest/MetroMesh.Telemetry.Ingest.csproj -c Release -o /app/publish --nologo

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS run
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MetroMesh.Telemetry.Ingest.dll"]
