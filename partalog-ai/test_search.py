"""
Partalog AI - TEST SEARCH
Görevi: search_vector_db ve exact_match_search fonksiyonlarını test eder.
"""

import asyncio
from services.embedding import get_text_embedding
from services.vector_db import search_vector_db, exact_match_search

async def test_searches():
    print("=== EXACT MATCH TEST ===")
    results = await exact_match_search(part_code="B2424", limit=3)
    for r in results:
        print(f"  - {r.get('PartCode')} | {r.get('PartName')} | sim={r.get('similarity')}")

    print("\n=== VECTOR SEARCH TEST ===")
    query = "overlok iğne barı metal"
    vector = await get_text_embedding(query)
    if vector:
        results = await search_vector_db(query_vector=vector, limit=3, min_similarity=0.3)
        for r in results:
            print(f"  - {r.get('PartCode')} | {r.get('PartName')} | sim={r.get('similarity'):.4f}")
    else:
        print("  Embedding alınamadı!")

if __name__ == "__main__":
    asyncio.run(test_searches())