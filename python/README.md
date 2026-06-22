[Portfolio Home: Joseph C. Dillard Geospatial Project Stack](https://josephdillard.github.io/JosephDillard/)

# Geospatial Data Gateway Python Worker

This package contains the geospatial ingest worker used by the gateway API. It can
inspect supported GIS files, normalize them to `EPSG:4326`, and load them into
PostGIS with gateway metadata columns.

It is the worker-side half of the public Geospatial Data Gateway repo:

- Root repo README: [../README.md](../README.md)
- Architecture notes: [../docs/architecture.md](../docs/architecture.md)
- Ingest contract: [../docs/ingest-contract.md](../docs/ingest-contract.md)

In the larger stack, this worker loads validated layers that GeoServer can
publish and the Geospatial Status Board can refresh through gateway events.
