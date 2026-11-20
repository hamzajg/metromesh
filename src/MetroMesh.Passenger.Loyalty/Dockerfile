FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY MetroMesh.slnx ./
COPY src/ ./src/
RUN dotnet publish src/MetroMesh.Passenger.Loyalty/MetroMesh.Passenger.Loyalty.csproj -c Release -o /app/publish --nologo

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS run
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MetroMesh.Passenger.Loyalty.dll"]
