# Geospatial Data Gateway Python Worker

This package contains the geospatial ingest worker used by the gateway API. It can
inspect supported GIS files, normalize them to `EPSG:4326`, and load them into
PostGIS with gateway metadata columns.
