FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/Geospatial.DataGateway.Api/Geospatial.DataGateway.Api.csproj ./Geospatial.DataGateway.Api/
RUN dotnet restore ./Geospatial.DataGateway.Api/Geospatial.DataGateway.Api.csproj

COPY src/Geospatial.DataGateway.Api ./Geospatial.DataGateway.Api
RUN dotnet publish ./Geospatial.DataGateway.Api/Geospatial.DataGateway.Api.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Geospatial.DataGateway.Api.dll"]
