from __future__ import annotations

from dataclasses import asdict, dataclass
from typing import Any


@dataclass(frozen=True)
class IngestSummary:
    source_uri: str
    feature_count: int
    geometry_type: str | None
    source_crs: str | None
    target_crs: str
    bounds: tuple[float, float, float, float] | None
    columns: list[str]

    def to_dict(self) -> dict[str, Any]:
        payload = asdict(self)
        if self.bounds is not None:
            payload["bounds"] = list(self.bounds)
        return payload


@dataclass(frozen=True)
class PostgisLoadResult:
    schema: str
    table: str
    feature_count: int
    geometry_type: str | None
    target_crs: str
