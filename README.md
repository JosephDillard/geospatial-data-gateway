# Geospatial Data Gateway

Geospatial Data Gateway is the intake and streaming control plane for the companion
GeoAI and status-board projects. It accepts geospatial files or feed references,
validates and normalizes them, loads clean layers into PostGIS, and exposes job
state for downstream map applications.

This repo is designed to sit beside:

- `geospatial-etl-validation-toolkit` - Pre-load data readiness checks and handoff reports.
- `geoai-asset-detection-platform` - Python GeoAI workflows that create vector detections.
- `geospatial-status-board` - Grails, GeoServer, PostGIS, and MapLibre status map.

## Repository Map

This repo is the ingest and eventing layer in the public geospatial stack. It
accepts files or feed references, records job state, loads clean layers to
PostGIS, and notifies downstream maps when layers are ready.

- [Portfolio site](https://josephdillard.github.io/JosephDillard/)
- [Geospatial Data Gateway repo](https://github.com/JosephDillard/geospatial-data-gateway)
- [Geospatial Status Board](https://github.com/JosephDillard/geospatial-status-board)
- [GeoAI Asset Detection Platform](https://github.com/JosephDillard/geoai-asset-detection-platform)
- [Geospatial ETL Validation Toolkit](https://github.com/JosephDillard/geospatial-etl-validation-toolkit)
- [Geospatial MCP Services](https://github.com/JosephDillard/geospatial-mcp-services)

## What This Provides

- ASP.NET Core API for dataset registration, ingest job tracking, health checks, and
  future live update events.
- Python ingest worker for GeoJSON, GeoPackage, zipped Shapefile, and CSV lat/lon
  sources.
- PostGIS schema for gateway metadata, ingest jobs, source catalog entries, and layer
  load events.
- Example local Docker stack for API, worker tooling, and PostGIS.
- Contracts that make it easy for the status-board web view to refresh layers when
  new data arrives.

## Target Architecture

```text
GeoJSON / GPKG / Shapefile / CSV / feeds
        |
        v
ASP.NET Core API
datasets, ingest jobs, status, live events
        |
        v
Python ingest worker
GeoPandas / GDAL / Shapely / SQLAlchemy
        |
        v
PostGIS
gateway metadata + publishable feature tables
        |
        v
GeoServer + Geospatial Status Board
WFS/GeoJSON layers today, live refresh later
```

## Repository Layout

```text
src/Geospatial.DataGateway.Api/   ASP.NET Core API and SignalR hub
python/geospatial_data_gateway/   Python geospatial ingest worker package
sql/                              PostGIS schema and helper SQL
docker/                           Container build files and PostGIS init scripts
docs/                             Architecture and API/worker contracts
examples/                         Small sample input files
```

## API

The API project is intentionally small and operational:

- `GET /health`
- `GET /datasets`
- `POST /datasets`
- `GET /ingest-jobs`
- `GET /ingest-jobs/{jobId}`
- `POST /ingest-jobs`
- `PATCH /ingest-jobs/{jobId}`
- `POST /ingest-jobs/{jobId}/events`
- `GET /hubs/geospatial-updates` for SignalR clients
- `POST /demo/layer-refresh` to broadcast a local demo map refresh event

The API stores its own metadata under the `geomain` schema by default. Feature tables
can be loaded into `public` or another configured schema so GeoServer can publish
them as map layers.

## Local SignalR Map Demo

The gateway hosts a self-contained SignalR hub at:

```text
http://localhost:7070/hubs/geospatial-updates
```

The companion status-board map can subscribe directly to this hub and refresh a WFS
layer when the gateway broadcasts `layer.refresh_requested`.

With the status board running at `http://localhost:18088/GeoStatusBoard`, start the
gateway API against the status-board Docker PostGIS network:

```powershell
docker build -t geospatial-data-gateway-api:dev -f src/Geospatial.DataGateway.Api/Dockerfile .
docker run -d --name gdg-api-local `
  --network geospatial-status-board_default `
  -p 7070:8080 `
  -e ASPNETCORE_URLS=http://+:8080 `
  -e ConnectionStrings__Postgis="Host=postgis;Port=5432;Database=geostatusboard;Username=gsb;Password=gsb" `
  -e Gateway__Cors__AllowedOrigins__0=http://localhost:18088 `
  -e Gateway__Cors__AllowedOrigins__1=http://127.0.0.1:18088 `
  geospatial-data-gateway-api:dev
```

Trigger a live map refresh:

```powershell
Invoke-RestMethod `
  -Method Post `
  -Uri http://localhost:7070/demo/layer-refresh `
  -ContentType 'application/json' `
  -Body '{"layerKey":"detectedRoads","message":"Manual local SignalR demo refresh."}'
```

The event is local-only: browser -> gateway SignalR hub -> status-board map. It does
not use Azure SignalR or any other hosted relay.

## Python Worker

The worker package handles the GIS-heavy work:

- Inspect a dataset and report geometry type, CRS, bounds, feature count, and columns.
- Reproject to `EPSG:4326`.
- Convert CSV files with latitude/longitude columns into point layers.
- Load normalized features into PostGIS with gateway metadata columns.

Install locally:

```powershell
cd python
python -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install --upgrade pip
python -m pip install -e ".[dev]"
```

Inspect a sample:

```powershell
geospatial-data-gateway inspect ..\examples\sample-sites.geojson
```

Load into PostGIS:

```powershell
geospatial-data-gateway load-postgis `
  --source ..\examples\sample-sites.geojson `
  --database-url postgresql+psycopg://gsb:gsb@localhost:5432/geostatusboard `
  --schema public `
  --table gateway_sample_sites `
  --dataset-name sample-sites `
  --if-exists replace
```

## Docker

The local stack is optional and aimed at integration testing:

```powershell
docker compose up --build
```

Default endpoints:

```text
API:     http://localhost:7070
PostGIS: localhost:5432/geostatusboard
```

Default local credentials:

```text
PostGIS user/password: gsb / gsb
```

## Companion Workflow

1. Register or submit a source through this gateway.
2. The Python worker validates and loads it into PostGIS.
3. GeoServer publishes the resulting feature table.
4. The status board consumes the layer through WFS.
5. Later, the status board can subscribe to SignalR events and refresh layers as soon
   as a gateway job finishes.

See [docs/architecture.md](docs/architecture.md) and
[docs/ingest-contract.md](docs/ingest-contract.md) for the current contract.
