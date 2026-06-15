from __future__ import annotations

from pathlib import Path
from typing import Iterable

from geospatial_data_gateway.models import IngestSummary

LATITUDE_CANDIDATES = ("latitude", "lat", "y")
LONGITUDE_CANDIDATES = ("longitude", "lon", "lng", "long", "x")


def inspect_source(source: str | Path, target_crs: str = "EPSG:4326") -> IngestSummary:
    frame = read_source(source, target_crs=target_crs)
    geometry_type = _first_geometry_type(frame.geometry.geom_type.dropna().unique())
    bounds = tuple(float(value) for value in frame.total_bounds) if len(frame) else None
    return IngestSummary(
        source_uri=str(source),
        feature_count=len(frame),
        geometry_type=geometry_type,
        source_crs=str(frame.crs) if frame.crs else None,
        target_crs=target_crs,
        bounds=bounds,
        columns=[column for column in frame.columns if column != frame.geometry.name],
    )


def read_source(source: str | Path, target_crs: str = "EPSG:4326"):
    import geopandas as gpd

    source_path = Path(source)
    suffix = source_path.suffix.lower()
    if suffix == ".csv":
        frame = _read_csv_points(source_path)
    else:
        frame = gpd.read_file(source_path)

    if frame.empty:
        return frame.set_crs(target_crs, allow_override=True) if frame.crs is None else frame

    if frame.crs is None:
        frame = frame.set_crs(target_crs)
    elif str(frame.crs) != target_crs:
        frame = frame.to_crs(target_crs)

    frame = frame[frame.geometry.notna()].copy()
    frame.geometry = frame.geometry.make_valid()
    return frame


def _read_csv_points(source_path: Path):
    import geopandas as gpd
    import pandas as pd

    frame = pd.read_csv(source_path)
    latitude = _find_column(frame.columns, LATITUDE_CANDIDATES)
    longitude = _find_column(frame.columns, LONGITUDE_CANDIDATES)
    if not latitude or not longitude:
        raise ValueError("CSV sources must include latitude/longitude columns.")

    return gpd.GeoDataFrame(
        frame,
        geometry=gpd.points_from_xy(frame[longitude], frame[latitude]),
        crs="EPSG:4326",
    )


def _find_column(columns: Iterable[str], candidates: tuple[str, ...]) -> str | None:
    by_lower = {column.lower(): column for column in columns}
    for candidate in candidates:
        if candidate in by_lower:
            return by_lower[candidate]
    return None


def _first_geometry_type(values) -> str | None:
    if len(values) == 0:
        return None
    if len(values) == 1:
        return str(values[0])
    return "GeometryCollection"
