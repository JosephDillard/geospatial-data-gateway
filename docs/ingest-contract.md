# Ingest Contract

This document defines the initial contract between the API, Python worker, PostGIS,
and map consumers.

## Dataset

A dataset is a logical source of geospatial information. One dataset can have many
ingest jobs over time.

```json
{
  "name": "sample-sites",
  "description": "Example point layer for smoke testing",
  "sourceUri": "examples/sample-sites.geojson",
  "sourceType": "geojson"
}
```

## Ingest Job

An ingest job is a request to inspect, normalize, and load a source.

```json
{
  "datasetName": "sample-sites",
  "sourceUri": "examples/sample-sites.geojson",
  "sourceType": "geojson",
  "targetSchema": "public",
  "targetTable": "gateway_sample_sites",
  "ifExists": "replace",
  "notes": "Local smoke test"
}
```

Valid `ifExists` values:

- `fail`
- `replace`
- `append`

Valid status values:

- `queued`
- `running`
- `loaded`
- `failed`
- `cancelled`

## Worker Output

The worker records the final loaded layer information:

```json
{
  "jobId": "9e86ef80-f1ea-4524-8df2-93c77f0fbf15",
  "status": "loaded",
  "featureCount": 14,
  "targetSchema": "public",
  "targetTable": "gateway_sample_sites",
  "geometryType": "Point",
  "crs": "EPSG:4326",
  "bounds": [-106.67, 35.02, -106.5, 35.15]
}
```

## Live Events

The API can fan out events through SignalR at `/hubs/geospatial-updates`.

Initial event names:

- `ingest.job.created`
- `ingest.job.updated`
- `ingest.job.loaded`
- `ingest.job.failed`
- `layer.refresh_requested`

The status board can subscribe to these later and refresh a configured GeoServer WFS
layer when `layer.refresh_requested` arrives.
