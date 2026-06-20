from __future__ import annotations

import sys
import unittest
from pathlib import Path

import httpx

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from api.ingestion import router as ingestion_router  # noqa: E402
from fastapi import FastAPI  # noqa: E402
from services.search_text_builder import (
    build_catalog_item_search_text,
    build_catalog_item_search_texts,
    build_context_aware_catalog_item_search_text,
    build_legacy_catalog_item_search_text,
)


class SearchTextBuilderTests(unittest.IsolatedAsyncioTestCase):
    def test_build_catalog_item_search_text_includes_weighted_fields(self) -> None:
        text = build_catalog_item_search_text(
            {
                "PartName": "Lower Knife",
                "Description": "Cuts thread near needle",
                "PartCode": "B2421-280-000",
                "RefNumber": "12",
                "Dimensions": "M5 3x3",
                "Mechanism": "Needle / cutting mechanism",
                "MachineBrand": "JUKI",
                "MachineModel": "DDL-8700",
                "MachineGroup": "Lockstitch",
            }
        )

        self.assertIn("Katalog Parça Adı: Lower Knife", text)
        self.assertIn("Uyumlu Makine: JUKI DDL-8700", text)
        self.assertIn("Makine Grubu: Lockstitch", text)
        self.assertIn("İşlevsel Kategori: Kesim Mekanizması -> Bıçak", text)
        self.assertIn("Parça Tanımı ve Fonksiyonu: Cuts thread near needle Needle / cutting mechanism M5 3x3", text)
        self.assertIn("Parça Kodu: B2421-280-000", text)
        self.assertIn("Referans No: 12", text)

    def test_build_catalog_item_search_text_deduplicates_and_drops_noise(self) -> None:
        text = build_catalog_item_search_text(
            {
                "PartName": "Needle Plate",
                "Description": " needle   plate ",
                "PartCode": "NP-001",
                "RefNumber": "0",
                "Dimensions": "N/A",
                "Mechanism": "Başlıksız",
                "MachineBrand": "JUKI",
                "MachineModel": "JUKI",
                "MachineGroup": "General",
            }
        )

        self.assertIn("Katalog Parça Adı: Needle Plate", text)
        self.assertIn("Parça Kodu: NP-001", text)
        self.assertIn("Uyumlu Makine: JUKI", text)
        self.assertIn("İşlevsel Kategori: Dikiş Oluşturma -> İğne plakası", text)
        self.assertNotIn("Parça Tanımı ve Fonksiyonu:", text)
        self.assertNotIn("Ölçü:", text)
        self.assertNotIn("Mekanizma:", text)
        self.assertNotIn("Referans No:", text)
        self.assertNotIn("General", text)

    def test_context_aware_search_text_separates_model_plate_from_wind_guide_plate(self) -> None:
        model_plate = build_context_aware_catalog_item_search_text(
            {
                "PartName": "MODEL PLAKASI (AŞAĞIYA BAKINIZ)",
                "PartCode": "4109410",
                "RefNumber": "33",
                "MachineBrand": "YAMATO",
                "MachineModel": "VG2500-8F",
            }
        )
        wind_guide_plate = build_context_aware_catalog_item_search_text(
            {
                "PartName": "RÜZGAR KILAVUZ PLAKASI",
                "PartCode": "3500007",
                "RefNumber": "24",
                "MachineBrand": "YAMATO",
                "MachineModel": "VG2500-8F",
            }
        )

        self.assertIn("İşlevsel Kategori: Kimlik ve Etiketleme -> Model plakası", model_plate)
        self.assertIn("İşlevsel Kategori: Kılavuz ve Yönlendirme -> Rüzgar kılavuz plakası", wind_guide_plate)

    def test_functional_category_prefers_part_name_over_page_mechanism(self) -> None:
        text = build_catalog_item_search_text(
            {
                "PartName": "VİDA M4-0.7X8",
                "Description": "MISCELLANEOUS COVERS(1)",
                "PartCode": "110013",
                "RefNumber": "25",
                "MachineBrand": "YAMATO",
                "MachineModel": "VG2500-8F",
            }
        )

        self.assertIn("İşlevsel Kategori: Bağlantı Elemanları -> Vida", text)
        self.assertNotIn("İşlevsel Kategori: Gövde ve Kapak", text)

    def test_build_legacy_catalog_item_search_text_keeps_previous_dry_run_comparison_shape(self) -> None:
        text = build_legacy_catalog_item_search_text(
            {
                "PartName": "Needle Plate",
                "Description": "Main plate",
                "PartCode": "NP-001",
                "Dimensions": "M5 3x3",
            }
        )

        self.assertEqual(text, "Needle Plate | Main plate | NP-001")

    def test_batch_builder_accepts_external_snake_case_contract(self) -> None:
        search_texts = build_catalog_item_search_texts(
            [
                {
                    "part_name": "Lower Knife",
                    "machine_brand_model": "JUKI DDL-8700",
                    "category": "Lockstitch",
                    "description": "Cuts thread near needle",
                    "part_code": "B2421-280-000",
                    "ref_no": "12",
                    "dimensions": "M5 3x3",
                    "mechanism": "Needle / cutting mechanism",
                }
            ]
        )

        self.assertEqual(len(search_texts), 1)
        text = search_texts[0]
        self.assertIn("Katalog Parça Adı: Lower Knife", text)
        self.assertIn("Uyumlu Makine: JUKI DDL-8700", text)
        self.assertIn("Makine Grubu: Lockstitch", text)
        self.assertIn("İşlevsel Kategori: Kesim Mekanizması -> Bıçak", text)
        self.assertIn("Parça Kodu: B2421-280-000", text)
        self.assertIn("Referans No: 12", text)

    async def test_build_search_texts_endpoint_returns_ordered_batch(self) -> None:
        app = FastAPI()
        app.include_router(ingestion_router, prefix="/api/v1/ingestion")
        transport = httpx.ASGITransport(app=app)

        async with httpx.AsyncClient(transport=transport, base_url="http://testserver") as client:
            response = await client.post(
                "/api/v1/ingestion/build-search-texts",
                json=[
                    {
                        "part_name": "MODEL PLAKASI",
                        "machine_brand_model": "YAMATO VG2500-8F",
                        "part_code": "4109410",
                        "ref_no": "33",
                    },
                    {
                        "part_name": "RÜZGAR KILAVUZ PLAKASI",
                        "machine_brand_model": "YAMATO VG2500-8F",
                        "part_code": "3500007",
                        "ref_no": "24",
                    },
                ],
            )

        self.assertEqual(response.status_code, 200)
        payload = response.json()
        self.assertEqual(len(payload), 2)
        self.assertIn("Model plakası", payload[0])
        self.assertIn("Rüzgar kılavuz plakası", payload[1])


if __name__ == "__main__":
    unittest.main()
