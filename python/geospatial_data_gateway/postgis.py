from __future__ import annotations

from datetime import datetime, timezone
from pathlib import Path
from uuid import UUID

from geoalchemy2 import Geometry
from sqlalchemy import create_engine, text

from geospatial_data_gateway.models import PostgisLoadResult
from geospatial_data_gateway.readers import read_source
from geospatial_data_gateway.validation import normalize_if_exists, require_identifier


def load_source_to_postgis(
    source: str | Path,
    database_url: str,
    schema: str,
    table: str,
    if_exists: str = "fail",
    dataset_name: str | None = None,
    dataset_id: UUID | None = None,
    job_id: UUID | None = None,
    source_name: str | None = None,
    target_crs: str = "EPSG:4326",
    metadata_schema: str = "geomain",
) -> PostgisLoadResult:
    schema = require_identifier(schema, "schema")
    table = require_identifier(table, "table")
    metadata_schema = require_identifier(metadata_schema, "metadata schema")
    if_exists = normalize_if_exists(if_exists)

    frame = read_source(source, target_crs=target_crs)
    geometry_type = _geometry_type(frame)
    source_name = source_name or dataset_name or Path(source).stem

    frame = frame.rename_geometry("geom")
    frame["dataset_id"] = str(dataset_id) if dataset_id else None
    frame["job_id"] = str(job_id) if job_id else None
    frame["source_name"] = source_name
    frame["loaded_at"] = datetime.now(timezone.utc)

    engine = create_engine(database_url)
    with engine.begin() as connection:
        connection.execute(text(f"CREATE SCHEMA IF NOT EXISTS {schema}"))
        connection.execute(text(f"CREATE SCHEMA IF NOT EXISTS {metadata_schema}"))
        _ensure_metadata_tables(connection, metadata_schema)

    if not frame.empty:
        geometry_dtype = Geometry("GEOMETRY", srid=_epsg_or_unknown(frame.crs))
        frame.to_postgis(
            table,
            engine,
            schema=schema,
            if_exists=if_exists,
            index=False,
            dtype={"geom": geometry_dtype},
        )

    with engine.begin() as connection:
        _record_layer_load(
            connection=connection,
            metadata_schema=metadata_schema,
            schema=schema,
            table=table,
            feature_count=len(frame),
            geometry_type=geometry_type,
            target_crs=target_crs,
            dataset_id=dataset_id,
            job_id=job_id,
        )

    return PostgisLoadResult(
        schema=schema,
        table=table,
        feature_count=len(frame),
        geometry_type=geometry_type,
        target_crs=target_crs,
    )


def _ensure_metadata_tables(connection, metadata_schema: str) -> None:
    connection.execute(
        text(
            f"""
            CREATE TABLE IF NOT EXISTS {metadata_schema}.datasets (
                dataset_id uuid PRIMARY KEY,
                name text NOT NULL UNIQUE,
                description text,
                source_uri text,
                source_type text,
                created_at timestamptz NOT NULL DEFAULT now(),
                updated_at timestamptz NOT NULL DEFAULT now()
            );

            CREATE TABLE IF NOT EXISTS {metadata_schema}.ingest_jobs (
                job_id uuid PRIMARY KEY,
                dataset_id uuid REFERENCES {metadata_schema}.datasets(dataset_id),
                dataset_name text,
                source_uri text NOT NULL,
                source_type text,
                target_schema text NOT NULL DEFAULT 'public',
                target_table text NOT NULL,
                if_exists text NOT NULL DEFAULT 'fail',
                status text NOT NULL DEFAULT 'queued',
                feature_count bigint NOT NULL DEFAULT 0,
                geometry_type text,
                source_crs text,
                target_crs text NOT NULL DEFAULT 'EPSG:4326',
                bounds jsonb,
                notes text,
                error_message text,
                created_at timestamptz NOT NULL DEFAULT now(),
                started_at timestamptz,
                finished_at timestamptz,
                updated_at timestamptz NOT NULL DEFAULT now()
            );

            CREATE TABLE IF NOT EXISTS {metadata_schema}.ingest_events (
                event_id bigserial PRIMARY KEY,
                job_id uuid REFERENCES {metadata_schema}.ingest_jobs(job_id) ON DELETE CASCADE,
                event_type text NOT NULL,
                message text,
                payload jsonb,
                created_at timestamptz NOT NULL DEFAULT now()
            );

            CREATE TABLE IF NOT EXISTS {metadata_schema}.layer_loads (
                load_id bigserial PRIMARY KEY,
                job_id uuid REFERENCES {metadata_schema}.ingest_jobs(job_id) ON DELETE SET NULL,
                dataset_id uuid REFERENCES {metadata_schema}.datasets(dataset_id) ON DELETE SET NULL,
                target_schema text NOT NULL,
                target_table text NOT NULL,
                feature_count bigint NOT NULL DEFAULT 0,
                geometry_type text,
                crs text NOT NULL DEFAULT 'EPSG:4326',
                loaded_at timestamptz NOT NULL DEFAULT now()
            );
            """
        )
    )


def _record_layer_load(
    connection,
    metadata_schema: str,
    schema: str,
    table: str,
    feature_count: int,
    geometry_type: str | None,
    target_crs: str,
    dataset_id: UUID | None,
    job_id: UUID | None,
) -> None:
    connection.execute(
        text(
            f"""
            INSERT INTO {metadata_schema}.layer_loads
                (job_id, dataset_id, target_schema, target_table, feature_count, geometry_type, crs)
            VALUES
                (:job_id, :dataset_id, :target_schema, :target_table, :feature_count, :geometry_type, :crs)
            """
        ),
        {
            "job_id": str(job_id) if job_id else None,
            "dataset_id": str(dataset_id) if dataset_id else None,
            "target_schema": schema,
            "target_table": table,
            "feature_count": feature_count,
            "geometry_type": geometry_type,
            "crs": target_crs,
        },
    )

    if job_id:
        connection.execute(
            text(
                f"""
                UPDATE {metadata_schema}.ingest_jobs
                SET status = 'loaded',
                    feature_count = :feature_count,
                    geometry_type = :geometry_type,
                    target_crs = :target_crs,
                    finished_at = now(),
                    updated_at = now()
                WHERE job_id = :job_id
                """
            ),
            {
                "job_id": str(job_id),
                "feature_count": feature_count,
                "geometry_type": geometry_type,
                "target_crs": target_crs,
            },
        )


def _geometry_type(frame) -> str | None:
    if frame.empty:
        return None
    values = frame.geom_type.dropna().unique()
    if len(values) == 1:
        return str(values[0])
    return "GeometryCollection"


def _epsg_or_unknown(crs) -> int:
    if crs is None:
        return -1
    epsg = crs.to_epsg()
    return epsg if epsg is not None else -1
