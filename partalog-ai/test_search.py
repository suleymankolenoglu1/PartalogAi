"""
Partalog AI - TEST SEARCH (Hybrid Search Test)
Görevi: exact_match_search ve search_vector_db fonksiyonlarını test eder.
"""

import asyncio
from services.vector_db import search_vector_db, exact_match_search
from services.embedding import get_text_embedding

async def test_exact_match():
    print("=" * 50)
    print("1️⃣ EXACT MATCH (Kod ile arama):")
    print("=" * 50)
    # Gerçek bir parça kodu ile test et
    results = await exact_match_search(part_code="B2424", limit=5)
    if results:
        for r in results:
            print(f"   ✅ {r.get('PartCode')} | {r.get('PartName')} | {r.get('MachineBrand')}")
    else:
        print("   ⚠️ Sonuç yok (DB bağlantısını ve parça kodunu kontrol et)")

async def test_vector_search():
    print("\n" + "=" * 50)
    print("2️⃣ VECTOR SEARCH (Semantik arama):")
    print("=" * 50)
    query = "Juki overlok iğne barı metal silindirik"
    print(f"   Sorgu: '{query}'")
    
    vector = get_text_embedding(query)
    if not vector:
        print("   ❌ Embedding alınamadı (GEMINI_API_KEY kontrol et)")
        return
    
    print(f"   ✅ Embedding boyutu: {len(vector)}")
    
    results = await search_vector_db(query_vector=vector, limit=5)
    if results:
        for r in results:
            sim = r.get('similarity', 0)
            print(f"   🔍 {r.get('PartCode')} | {r.get('PartName')} | benzerlik: {sim:.4f}")
    else:
        print("   ⚠️ Sonuç yok")

if __name__ == "__main__":
    asyncio.run(test_exact_match())
    asyncio.run(test_vector_search())