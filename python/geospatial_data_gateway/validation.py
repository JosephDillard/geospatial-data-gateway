from __future__ import annotations

import re

IDENTIFIER_PATTERN = re.compile(r"^[A-Za-z_][A-Za-z0-9_]*$")
IF_EXISTS_VALUES = {"fail", "replace", "append"}


def require_identifier(value: str, label: str) -> str:
    trimmed = value.strip()
    if not IDENTIFIER_PATTERN.match(trimmed):
        raise ValueError(f"{label} must be a valid unquoted SQL identifier: {value!r}")
    return trimmed


def normalize_if_exists(value: str) -> str:
    normalized = value.strip().lower()
    if normalized not in IF_EXISTS_VALUES:
        raise ValueError("if_exists must be one of: fail, replace, append")
    return normalized
