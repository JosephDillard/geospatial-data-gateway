# Agent Guide

This repo is the ingestion and live-refresh control plane in the companion
geospatial stack. It accepts geospatial sources, validates and normalizes them,
loads feature tables into PostGIS, and exposes API/SignalR endpoints for map
applications.

## Stack Context

Related sibling repos:

- `geoai-asset-detection-platform` can produce vector detections that this
  gateway may load or register.
- `geospatial-status-board` visualizes PostGIS/GeoServer layers and subscribes
  to gateway refresh events.
- `geospatial-mcp-services` provides map-aware assistant tools that may later
  consume gateway/status-board context.

When changing cross-repo docs, prefer full GitHub URLs for links that point
outside this repo.

## What This Repo Owns

- ASP.NET Core API: `src/Geospatial.DataGateway.Api/`
- SignalR hub: `src/Geospatial.DataGateway.Api/GeospatialUpdatesHub.cs`
- Python ingest worker: `python/geospatial_data_gateway/`
- Worker tests: `python/tests/`
- PostGIS schema and helper SQL: `sql/`
- API and ingest contracts: `docs/`
- Sample input data: `examples/`
- Local containers: `docker-compose.yml`, `docker/`, and API Dockerfile

The API stores gateway metadata separately from publishable feature tables.
Feature tables should be loadable into a schema GeoServer can publish.

## Development Notes

- Keep the .NET API focused on dataset/job state, health, and local SignalR
  events.
- Keep GIS-heavy source inspection, reprojection, validation, and PostGIS loading
  in the Python worker.
- Normalize web-map-facing geometries to EPSG:4326 unless the ingest contract
  explicitly says otherwise.
- The status-board map expects refresh payloads such as
  `layer.refresh_requested` with a stable `layerKey`.
- This project is intentionally local/self-hosted. Do not add hosted relays or
  external paid services without an explicit user request.
- Keep credentials in `.env` or environment variables, not in committed files.

## Useful Commands

.NET API:

```powershell
dotnet build Geospatial.DataGateway.sln
dotnet run --project src\Geospatial.DataGateway.Api
```

Python worker:

```powershell
cd python
python -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install --upgrade pip
python -m pip install -e ".[dev]"
python -m pytest
python -m ruff check geospatial_data_gateway tests
```

Local Docker stack:

```powershell
docker compose up --build
```

## Before Finishing Changes

- Run `dotnet build Geospatial.DataGateway.sln` after API changes.
- Run Python tests after worker, validation, reader, or PostGIS changes.
- Update `docs/architecture.md`, `docs/ingest-contract.md`, and `README.md`
  when endpoints, events, database schema, or load behavior change.
