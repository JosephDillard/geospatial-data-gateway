from __future__ import annotations

import unittest

from geospatial_data_gateway.validation import normalize_if_exists, require_identifier


class ValidationTests(unittest.TestCase):
    def test_require_identifier_accepts_plain_sql_identifiers(self) -> None:
        self.assertEqual(require_identifier("public", "schema"), "public")
        self.assertEqual(require_identifier("gateway_sample_sites", "table"), "gateway_sample_sites")

    def test_require_identifier_rejects_unsafe_values(self) -> None:
        with self.assertRaises(ValueError):
            require_identifier("public.detected_roads", "table")
        with self.assertRaises(ValueError):
            require_identifier("table-name", "table")

    def test_normalize_if_exists(self) -> None:
        self.assertEqual(normalize_if_exists(" Replace "), "replace")
        with self.assertRaises(ValueError):
            normalize_if_exists("merge")


if __name__ == "__main__":
    unittest.main()
