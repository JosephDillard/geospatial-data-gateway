FROM python:3.12-slim

ENV PYTHONDONTWRITEBYTECODE=1
ENV PYTHONUNBUFFERED=1

RUN apt-get update \
    && apt-get install -y --no-install-recommends gdal-bin libgdal-dev build-essential \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /workspace
COPY python/pyproject.toml ./python/pyproject.toml
COPY python/geospatial_data_gateway ./python/geospatial_data_gateway
RUN python -m pip install --upgrade pip \
    && python -m pip install -e "./python"

ENTRYPOINT ["geospatial-data-gateway"]
CMD ["--help"]
