"""
Deterministic CatalogItem search text builder.

This mirrors the .NET CatalogItemSearchTextBuilder so new catalog processing
and Python backfills generate embeddings from the same weighted field strategy.
"""

from __future__ import annotations

import re
from typing import Mapping


_GENERIC_VALUES = {
    "-",
    "--",
    "0",
    "n/a",
    "na",
    "null",
    "none",
    "unknown",
    "bilinmeyen",
    "untitled",
    "basliksiz",
    "başlıksız",
    "general",
    "genel",
    "page",
    "sayfa",
    "catalog",
    "katalog",
    "parts",
    "part list",
    "parts list",
    "spare parts",
    "technical drawing",
}


def build_catalog_item_search_text(row: Mapping[str, object | None]) -> str:
    return build_context_aware_catalog_item_search_text(row)


def build_catalog_item_search_texts(rows: list[Mapping[str, object | None]]) -> list[str]:
    """Build canonical search text for a batch of external ingestion rows."""
    return [build_catalog_item_search_text(_normalize_ingestion_row(row)) for row in rows]


def build_context_aware_catalog_item_search_text(row: Mapping[str, object | None]) -> str:
    """Build the canonical context-aware text sent to the embedding model."""
    sections: list[str] = []
    seen: set[str] = set()

    category, sub_category = _infer_functional_category(row)
    part_definition = _build_part_definition(row)

    _add_section(sections, seen, "Katalog Parça Adı", row.get("PartName"))
    _add_section(sections, seen, "Uyumlu Makine", _build_machine_brand_model(row))
    _add_section(sections, seen, "Makine Grubu", row.get("MachineGroup"))
    _add_section(sections, seen, "İşlevsel Kategori", _format_category(category, sub_category))
    _add_section(sections, seen, "Parça Tanımı ve Fonksiyonu", part_definition)
    _add_section(sections, seen, "Mekanizma", row.get("Mechanism"))
    _add_section(sections, seen, "Ölçü", row.get("Dimensions"))
    _add_section(sections, seen, "Parça Kodu", row.get("PartCode"))
    _add_section(sections, seen, "Referans No", row.get("RefNumber"))

    return " | ".join(sections)


def build_legacy_catalog_item_search_text(row: Mapping[str, object | None]) -> str:
    parts = [
        _raw_clean(row.get("PartName")),
        _raw_clean(row.get("Description")),
        _raw_clean(row.get("PartCode")),
    ]
    return " | ".join(part for part in parts if part)


def _first_present(row: Mapping[str, object | None], *keys: str) -> object | None:
    for key in keys:
        value = row.get(key)
        if value is not None:
            return value
    return None


def _normalize_ingestion_row(row: Mapping[str, object | None]) -> dict[str, object | None]:
    machine_brand = _first_present(row, "MachineBrand", "machine_brand")
    machine_model = _first_present(row, "MachineModel", "machine_model")
    machine_brand_model = _first_present(row, "MachineBrandModel", "machine_brand_model")

    if not machine_brand and not machine_model and machine_brand_model:
        machine_brand = machine_brand_model

    return {
        "PartName": _first_present(row, "PartName", "part_name"),
        "Description": _first_present(row, "Description", "description"),
        "PartCode": _first_present(row, "PartCode", "part_code"),
        "RefNumber": _first_present(row, "RefNumber", "RefNo", "ref_number", "ref_no"),
        "Dimensions": _first_present(row, "Dimensions", "dimensions"),
        "Mechanism": _first_present(row, "Mechanism", "mechanism"),
        "MachineBrand": machine_brand,
        "MachineModel": machine_model,
        "MachineGroup": _first_present(row, "MachineGroup", "machine_group", "Category", "category"),
    }


def _build_context(row: Mapping[str, object | None]) -> str | None:
    values: list[str] = []
    seen: set[str] = set()
    for key in ("MachineBrand", "MachineModel", "MachineGroup"):
        value = _clean(row.get(key))
        if not value:
            continue
        normalized = _normalize_key(value)
        if normalized in seen:
            continue
        seen.add(normalized)
        values.append(value)
    return " ".join(values) if values else None


def _build_machine_brand_model(row: Mapping[str, object | None]) -> str | None:
    return _join_unique(row.get("MachineBrand"), row.get("MachineModel"))


def _build_part_definition(row: Mapping[str, object | None]) -> str | None:
    return _join_unique(row.get("Description"), row.get("Mechanism"), row.get("Dimensions"))


def _format_category(category: str | None, sub_category: str | None) -> str | None:
    if category and sub_category and _normalize_key(category) != _normalize_key(sub_category):
        return f"{category} -> {sub_category}"
    return category or sub_category


def _join_unique(*values: object | None) -> str | None:
    cleaned_values: list[str] = []
    seen: set[str] = set()
    for value in values:
        cleaned = _clean(value)
        if not cleaned:
            continue
        normalized = _normalize_key(cleaned)
        if normalized in seen:
            continue
        seen.add(normalized)
        cleaned_values.append(cleaned)
    return " ".join(cleaned_values) if cleaned_values else None


def _infer_functional_category(row: Mapping[str, object | None]) -> tuple[str | None, str | None]:
    part_name = _normalize_ascii(_raw_clean(row.get("PartName")) or "")
    supporting_text = " ".join(
        value for value in (_raw_clean(row.get("Description")), _raw_clean(row.get("Mechanism"))) if value
    )
    normalized = _normalize_ascii(" ".join(value for value in (part_name, supporting_text) if value))

    rules: tuple[tuple[tuple[str, ...], tuple[str, str]], ...] = (
        (("model", "name plate", "nameplate", "rating plate", "kimlik", "marka plak", "isim etiket"), ("Kimlik ve Etiketleme", "Model plakası")),
        (("seri", "serial"), ("Kimlik ve Etiketleme", "Seri numara plakası")),
        (("ruzgar", "kilavuz", "wind guide"), ("Kılavuz ve Yönlendirme", "Rüzgar kılavuz plakası")),
        (("igne plaka", "needle plate", "throat plate"), ("Dikiş Oluşturma", "İğne plakası")),
        (("igne", "needle"), ("Dikiş Oluşturma", "İğne mekanizması")),
        (("luper", "looper"), ("Dikiş Oluşturma", "Lüper mekanizması")),
        (("iplik", "thread"), ("İplik Yönetimi", "İplik kılavuzu")),
        (("kapak", "cover"), ("Gövde ve Kapak", "Kapak parçası")),
        (("plaka", "plate"), ("Plaka ve Gövde Elemanları", "Plaka")),
        (("vida", "screw"), ("Bağlantı Elemanları", "Vida")),
        (("pul", "washer"), ("Bağlantı Elemanları", "Pul")),
        (("rulman", "bearing"), ("Hareket Aktarım", "Rulman")),
        (("bıçak", "bicak", "knife", "cutter"), ("Kesim Mekanizması", "Bıçak")),
    )

    for needles, category in rules:
        if any(needle in part_name for needle in needles):
            return category

    for needles, category in rules:
        if any(needle in normalized for needle in needles):
            return category

    mechanism = _clean(row.get("Mechanism"))
    machine_group = _clean(row.get("MachineGroup"))
    if mechanism:
        return "Mekanizma", mechanism
    if machine_group:
        return "Makine Grubu", machine_group
    return None, None


def _normalize_ascii(value: str) -> str:
    translation = str.maketrans(
        {
            "ç": "c",
            "Ç": "c",
            "ğ": "g",
            "Ğ": "g",
            "ı": "i",
            "İ": "i",
            "ö": "o",
            "Ö": "o",
            "ş": "s",
            "Ş": "s",
            "ü": "u",
            "Ü": "u",
        }
    )
    return _normalize_key(value.translate(translation))


def _add_section(sections: list[str], seen: set[str], label: str, value: object | None) -> None:
    cleaned = _clean(value)
    if not cleaned:
        return

    normalized = _normalize_key(cleaned)
    if normalized in seen:
        return

    seen.add(normalized)
    sections.append(f"{label}: {cleaned}")


def _clean(value: object | None) -> str | None:
    cleaned = _raw_clean(value)
    if not cleaned:
        return None
    return None if _is_generic(cleaned) else cleaned


def _raw_clean(value: object | None) -> str | None:
    if value is None:
        return None

    cleaned = re.sub(r"\s+", " ", str(value).strip())
    cleaned = cleaned.strip(" |,;:./\\")
    return cleaned or None


def _is_generic(value: str) -> bool:
    return value.lower() in _GENERIC_VALUES or _normalize_key(value) in _GENERIC_VALUES


def _normalize_key(value: str) -> str:
    return re.sub(r"\s+", " ", value.strip().lower())
