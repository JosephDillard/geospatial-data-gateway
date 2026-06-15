from __future__ import annotations

import argparse
import json
import os
from uuid import UUID

from geospatial_data_gateway.readers import inspect_source


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="geospatial-data-gateway",
        description="Inspect and load geospatial data into PostGIS for the gateway.",
    )
    subcommands = parser.add_subparsers(dest="command", required=True)

    inspect_parser = subcommands.add_parser("inspect", help="Inspect a source dataset.")
    inspect_parser.add_argument("source")
    inspect_parser.add_argument("--target-crs", default="EPSG:4326")

    load_parser = subcommands.add_parser("load-postgis", help="Load a source dataset into PostGIS.")
    load_parser.add_argument("--source", required=True)
    load_parser.add_argument(
        "--database-url",
        default=os.getenv("GEOSPATIAL_GATEWAY_POSTGIS_URL"),
        help="SQLAlchemy PostGIS URL. Defaults to GEOSPATIAL_GATEWAY_POSTGIS_URL.",
    )
    load_parser.add_argument("--schema", default="public")
    load_parser.add_argument("--table", required=True)
    load_parser.add_argument("--if-exists", default="fail", choices=["fail", "replace", "append"])
    load_parser.add_argument("--dataset-name")
    load_parser.add_argument("--dataset-id", type=UUID)
    load_parser.add_argument("--job-id", type=UUID)
    load_parser.add_argument("--source-name")
    load_parser.add_argument("--target-crs", default="EPSG:4326")
    load_parser.add_argument("--metadata-schema", default="geomain")
    return parser


def main(argv: list[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)

    if args.command == "inspect":
        summary = inspect_source(args.source, target_crs=args.target_crs)
        print(json.dumps(summary.to_dict(), indent=2, sort_keys=True))
        return 0

    if args.command == "load-postgis":
        if not args.database_url:
            parser.error("--database-url or GEOSPATIAL_GATEWAY_POSTGIS_URL is required.")
        from geospatial_data_gateway.postgis import load_source_to_postgis

        result = load_source_to_postgis(
            source=args.source,
            database_url=args.database_url,
            schema=args.schema,
            table=args.table,
            if_exists=args.if_exists,
            dataset_name=args.dataset_name,
            dataset_id=args.dataset_id,
            job_id=args.job_id,
            source_name=args.source_name,
            target_crs=args.target_crs,
            metadata_schema=args.metadata_schema,
        )
        print(json.dumps(result.__dict__, indent=2, sort_keys=True))
        return 0

    parser.error(f"Unsupported command: {args.command}")
    return 2


if __name__ == "__main__":
    raise SystemExit(main())
