from __future__ import annotations

import sys
import unittest
from pathlib import Path
from unittest.mock import AsyncMock, patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from services.chat_context import (  # noqa: E402
    apply_model_compatibility_gate,
    clear_unmentioned_brand_guess,
    is_generic_symptom_query_without_context,
    looks_like_machine_catalog_question,
    resolve_search_scope,
    source_matches_resolved_scope,
    should_hold_generic_diagnosis_catalog_search,
)
from services.chat_matching import (  # noqa: E402
    filter_diagnosis_results,
    filter_strict_part_lookup_results,
    limit_context_related_sources,
)
from services.chat_intent import (  # noqa: E402
    ensure_unavailable_feature_notice,
    extract_code_from_text,
    get_intent_mode_block,
    normalize_intent_payload,
    unavailable_feature_label,
)
from services.chat_memory import (  # noqa: E402
    build_active_context_source,
    build_active_context_sources,
    detect_machine_group_from_text,
    extract_sticky_context_from_history,
    select_sticky_candidate_from_query,
)
from services.chat_parts import build_search_parts  # noqa: E402
from services.chat_policy import (  # noqa: E402
    resolve_confidence_thresholds,
    route_without_search,
    source_quality_early_response,
)
from services.chat_prompt import build_active_context_line  # noqa: E402
from services.chat_prompt import build_context_text  # noqa: E402
from services.chat_prompt import build_final_prompt  # noqa: E402
from services.chat_prompt import build_response_mode_instruction  # noqa: E402
from services.chat_retrieval import rewrite_user_query  # noqa: E402
from services.chat_responses import (  # noqa: E402
    build_deterministic_reply_from_sources,
    build_exact_code_reply_from_sources,
    build_no_result_guidance,
    classify_response_mode,
    sanitize_reply_safety_language,
)
from services.search_trace import build_search_trace  # noqa: E402
from services.chat_sources import (  # noqa: E402
    append_unique_sources,
    build_catalog_source,
    build_source_confidence,
    build_source_match_reason,
)
from services.chat_terms import (  # noqa: E402
    diagnosis_terms_for_query,
    expand_part_search_text,
    is_general_knowledge_intent,
    related_part_terms_for_context,
)
from services.chat_visual import (  # noqa: E402
    build_visual_hint_text,
    normalize_image_analysis,
    rerank_results_by_visual_hints,
)
from api.chat import _plan_limit_message  # noqa: E402
from config import settings  # noqa: E402


class SearchScopeResolutionTests(unittest.IsolatedAsyncioTestCase):
    async def test_resolve_search_scope_overrides_frontend_catalog_for_user_model(self) -> None:
        with patch(
            "services.chat_context.find_catalogs_by_machine",
            new=AsyncMock(
                return_value=[
                    {
                        "Id": "yamato-catalog",
                        "Name": "VG2500-8F Parts List",
                        "Brands": "Yamato",
                        "Models": "VG2500-8F",
                    }
                ]
            ),
        ):
            scope = await resolve_search_scope(
                "VG2500 model plakasının kodu nedir",
                ["juki-catalog"],
                [],
            )

        self.assertEqual(scope["resolved_catalog_ids"], ["yamato-catalog"])
        self.assertEqual(scope["resolved_brand"], "Yamato")
        self.assertEqual(scope["resolved_machine_model"], "VG2500")
        self.assertEqual(scope["scope_source"], "user_model_db_override")
        self.assertTrue(scope["frontend_scope_mismatch"])
        self.assertTrue(scope["reset_context"])


class QueryRewriteTests(unittest.IsolatedAsyncioTestCase):
    async def test_rewrite_user_query_returns_clean_catalog_keyword(self) -> None:
        class FakeResponse:
            status = 200

            async def __aenter__(self):
                return self

            async def __aexit__(self, exc_type, exc, tb):
                return False

            async def json(self):
                return {
                    "candidates": [
                        {
                            "content": {
                                "parts": [{"text": "model plakası"}],
                            }
                        }
                    ]
                }

        class FakeSession:
            def __init__(self, *args, **kwargs):
                pass

            async def __aenter__(self):
                return self

            async def __aexit__(self, exc_type, exc, tb):
                return False

            def post(self, *_args, **_kwargs):
                return FakeResponse()

        with (
            patch("services.chat_retrieval.provider.has_credentials", return_value=True),
            patch("services.chat_retrieval.provider.generate_content_url", return_value="https://example.test/generate"),
            patch("services.chat_retrieval.provider.build_headers", new=AsyncMock(return_value={})),
            patch("services.chat_retrieval.aiohttp.ClientSession", FakeSession),
        ):
            rewritten = await rewrite_user_query(
                "üstünde marka yazan böyle Yamato yazan plakanın kodu nedir",
                brand_context="Yamato",
            )

        self.assertEqual(rewritten, "model plakası")


class ChatBehaviorTests(unittest.TestCase):
    def test_dev_quota_bypass_skips_plan_limit_gate_when_debug_enabled(self) -> None:
        original_debug = settings.DEBUG
        original_bypass = settings.DEV_AI_QUOTA_BYPASS
        try:
            settings.DEBUG = True
            settings.DEV_AI_QUOTA_BYPASS = True

            self.assertIsNone(_plan_limit_message("CatalogOnly", 0, 0))
            self.assertIsNone(_plan_limit_message("CatalogWithAI", 5, 5))
        finally:
            settings.DEBUG = original_debug
            settings.DEV_AI_QUOTA_BYPASS = original_bypass

    def test_dev_quota_bypass_does_not_skip_when_debug_disabled(self) -> None:
        original_debug = settings.DEBUG
        original_bypass = settings.DEV_AI_QUOTA_BYPASS
        try:
            settings.DEBUG = False
            settings.DEV_AI_QUOTA_BYPASS = True

            self.assertEqual(
                _plan_limit_message("CatalogOnly", 0, 0),
                "AI sorgu limitinize ulaştınız, planınızı yükseltin",
            )
        finally:
            settings.DEBUG = original_debug
            settings.DEV_AI_QUOTA_BYPASS = original_bypass

    def test_price_hint_overrides_search_intent_when_router_misses_it(self) -> None:
        analysis = normalize_intent_payload(
            {
                "intent": "SEARCH",
                "part_code": "SM4041055SP",
            },
            "SM4041055SP fiyatı ne",
        )

        self.assertEqual(analysis["intent"], "PRICE")

    def test_stock_hint_overrides_chat_intent_when_router_misses_it(self) -> None:
        analysis = normalize_intent_payload(
            {
                "intent": "CHAT",
                "part_code": "SM4041055SP",
            },
            "SM4041055SP stokta var mı",
        )

        self.assertEqual(analysis["intent"], "STOCK")

    def test_fallback_intent_extracts_part_code_without_gemini(self) -> None:
        analysis = normalize_intent_payload(None, "SM4041055SP fiyatı ne")

        self.assertEqual(analysis["intent"], "PRICE")
        self.assertEqual(analysis["part_code"], "SM4041055SP")
        self.assertIsNone(analysis["part_name"])

    def test_search_parts_infer_model_plate_when_router_misses_part_name(self) -> None:
        analysis = {
            "intent": "SEARCH",
            "brand": None,
            "part_name": None,
            "machine_model": "VG2500",
            "parts": [],
        }

        parts = build_search_parts(
            analysis,
            intent="SEARCH",
            extracted_part=None,
            extracted_code=None,
            extracted_dimensions=None,
            extracted_context_part=None,
            raw_user_query="VG2500 model plakasının kodu nedir",
            user_query="VG2500 model plakasının kodu nedir",
        )

        self.assertEqual(parts[0]["part_name"], "model plakası")
        self.assertEqual(analysis["part_name"], "model plakası")

    def test_unmentioned_router_brand_guess_is_cleared(self) -> None:
        analysis = {"brand": "TYPICAL"}

        brand = clear_unmentioned_brand_guess(analysis, "VG2500 model plakasının kodu nedir", "TYPICAL")

        self.assertIsNone(brand)
        self.assertIsNone(analysis["brand"])

    def test_model_compatibility_gate_rejects_wrong_catalog(self) -> None:
        search_scope = {
            "resolved_machine_model": "VG2500",
            "resolved_catalog_ids": ["yamato-catalog"],
        }
        sources = [
            {"code": "4109410", "name": "MODEL PLAKASI", "catalogId": "yamato-catalog", "machine_model": "VG2500-8F"},
            {"code": "70003357", "name": "PLAKA_ARKA", "catalogId": "juki-catalog", "machine_model": "MF-7900"},
        ]

        accepted, rejected = apply_model_compatibility_gate(sources, search_scope)

        self.assertEqual([source["code"] for source in accepted], ["4109410"])
        self.assertEqual([source["code"] for source in rejected], ["70003357"])
        self.assertTrue(source_matches_resolved_scope(accepted[0], search_scope))
        self.assertFalse(source_matches_resolved_scope(rejected[0], search_scope))

    def test_code_extractor_handles_numeric_catalog_codes(self) -> None:
        self.assertEqual(extract_code_from_text("70003363 parçasını göster"), "70003363")

    def test_machine_catalog_route_ignores_part_plate_questions(self) -> None:
        self.assertTrue(looks_like_machine_catalog_question("VG2500 var mı özellikleri nelerdir", "CHAT"))
        self.assertFalse(looks_like_machine_catalog_question("peki model plakası nedir", "SEARCH"))

    def test_unavailable_feature_notice_is_appended_when_model_omits_it(self) -> None:
        reply = ensure_unavailable_feature_notice(
            "Ustam, SM4041055SP kodlu VİDA bulundu.",
            {"unsupported_feature": "fiyat"},
        )

        self.assertIn("fiyat bilgisi bu ekranda henüz aktif değil", reply)

    def test_price_intent_does_not_emit_price_values(self) -> None:
        reply = build_exact_code_reply_from_sources(
            "3101801 fiyatı ne",
            {
                "intent": "SEARCH",
                "original_intent": "PRICE",
                "unsupported_feature": "fiyat",
                "part_code": "3101801",
            },
            [
                {
                    "code": "3101801",
                    "name": "İğne Bağlantı Parçası",
                    "pageNumber": "3",
                    "price": 1250,
                }
            ],
        )

        self.assertIsNotNone(reply)
        self.assertIn("fiyat bilgisi bu ekranda henüz aktif değil", reply or "")
        self.assertIn("3101801", reply or "")
        self.assertNotIn("1250", reply or "")

    def test_stock_intent_does_not_emit_stock_status(self) -> None:
        reply = build_exact_code_reply_from_sources(
            "3101801 stokta var mı",
            {
                "intent": "SEARCH",
                "original_intent": "STOCK",
                "unsupported_feature": "stok",
                "part_code": "3101801",
            },
            [
                {
                    "code": "3101801",
                    "name": "İğne Bağlantı Parçası",
                    "pageNumber": "3",
                    "stockStatus": "Stokta Var",
                }
            ],
        )

        self.assertIsNotNone(reply)
        self.assertIn("stok bilgisi bu ekranda henüz aktif değil", reply or "")
        self.assertIn("3101801", reply or "")
        self.assertNotIn("Stokta Var", reply or "")

    def test_deterministic_reply_includes_page_and_ref_context(self) -> None:
        reply = build_deterministic_reply_from_sources(
            "vida ara",
            [
                {
                    "code": "160000",
                    "name": "Vida",
                    "brand": "YAMATO",
                    "pageNumber": "5",
                    "refNo": "12",
                }
            ],
        )

        self.assertIn("160000", reply)
        self.assertIn("Sf 5", reply)
        self.assertIn("Ref 12", reply)

    def test_source_match_reason_explains_why_candidate_was_suggested(self) -> None:
        source = build_catalog_source(
            {
                "PartCode": "40024796",
                "PartName": "KAYAR_KAPAK",
                "MachineBrand": "JUKI",
                "ViewerPageNumber": "4",
                "RefNumber": "13",
                "similarity": 0.76,
            },
            query="kayar kapak | slide cover",
            requested_code=None,
            shop_base_url="https://example.test/ara/",
            is_fallback=False,
            fallback_reason=None,
        )

        self.assertIn("matchReason", source)
        self.assertEqual(source["confidenceLabel"], "Yüksek aday")
        self.assertFalse(source["requiresVerification"])
        self.assertIn("kayar kapak araması bu parçayla eşleşti", source["matchReason"])
        self.assertIn("katalogda Sf 4, Ref 13 olarak görünüyor", source["matchReason"])

    def test_source_match_reason_marks_relaxed_filter_candidates(self) -> None:
        reason = build_source_match_reason(
            {
                "code": "70003363",
                "name": "İPLİK_GÜZERGÂHI",
                "pageNumber": "4",
                "refNo": "32",
                "query": "iplik kılavuzu",
                "similarity": 0.61,
                "fallback": True,
                "fallback_reason": "brand_removed",
            }
        )

        self.assertIn("marka filtresi kaldırılınca aday oldu", reason)
        self.assertIn("iplik kılavuzu aramasından aday oldu", reason)
        self.assertIn("katalogda Sf 4, Ref 32 olarak görünüyor", reason)

        confidence_label, requires_verification = build_source_confidence(
            {
                "similarity": 0.61,
                "fallback": True,
                "fallback_reason": "brand_removed",
            }
        )

        self.assertEqual(confidence_label, "Teyit gerekli")
        self.assertTrue(requires_verification)

    def test_context_text_includes_match_reason_for_model_prompt(self) -> None:
        context = build_context_text(
            [
                {
                    "code": "40024796",
                    "name": "KAYAR_KAPAK",
                    "brand": "JUKI",
                    "pageNumber": "4",
                    "refNo": "13",
                    "matchReason": "arama terimi: kayar kapak; katalog konumu Sf 4, Ref 13",
                }
            ]
        )

        self.assertIn("Önerme nedeni", context)
        self.assertIn("Güven: Orta aday", build_context_text([
            {
                "code": "70003363",
                "name": "İPLİK_GÜZERGÂHI",
                "brand": "JUKI",
                "pageNumber": "4",
                "refNo": "32",
                "confidenceLabel": "Orta aday",
            }
        ]))
        self.assertIn("arama terimi: kayar kapak", context)

    def test_append_unique_sources_enriches_source_explanation_contract(self) -> None:
        target = []
        append_unique_sources(
            target,
            [
                {
                    "code": "13403407",
                    "name": "KAPAK_DESTEK",
                    "brand": "JUKI",
                    "pageNumber": "4",
                    "refNo": "2",
                    "query": "kapak destek",
                    "similarity": 0.74,
                }
            ],
        )

        self.assertEqual(target[0]["code"], "13403407")
        self.assertIn("matchReason", target[0])
        self.assertIn("confidenceLabel", target[0])
        self.assertIn("requiresVerification", target[0])

    def test_unavailable_feature_labels_are_explicit(self) -> None:
        self.assertEqual(unavailable_feature_label("PRICE"), "fiyat")
        self.assertEqual(unavailable_feature_label("stock"), "stok")
        self.assertIsNone(unavailable_feature_label("SEARCH"))

    def test_image_analysis_normalization_keeps_structured_visual_hints(self) -> None:
        normalized = normalize_image_analysis(
            {
                "part_category": "plaka",
                "material_hint": "metal",
                "machine_type_hint": "reçme",
                "shape_tags": ["iki_delikli", "L_tip"],
                "visible_codes": "B2424-354-000",
                "detected_brand_text": "JUKI",
            }
        )

        self.assertEqual(normalized["part_family"], "plaka")
        self.assertEqual(normalized["material"], "metal")
        self.assertEqual(normalized["assembly_hint"], "reçme")
        self.assertEqual(normalized["shape_traits"], ["iki_delikli", "L_tip"])
        self.assertEqual(normalized["visible_code_tokens"], ["B2424-354-000"])
        self.assertEqual(normalized["brand_model_tokens"], ["JUKI"])

        hint_text = build_visual_hint_text(normalized)
        self.assertIn("plaka", hint_text)
        self.assertIn("iki_delikli", hint_text)
        self.assertIn("B2424-354-000", hint_text)

    def test_visual_hint_rerank_prefers_shape_and_context_matches(self) -> None:
        image_analysis = normalize_image_analysis(
            {
                "candidate_part_name": "plaka",
                "shape_traits": ["iki_delikli", "L_tip"],
                "assembly_hint": "iğne çevresi",
            }
        )
        rows = [
            {
                "PartCode": "B",
                "PartName": "Metal plaka",
                "Description": "düz bağlantı plakası",
                "VisualShapeTags": "[]",
            },
            {
                "PartCode": "A",
                "PartName": "İğne plakası",
                "Description": "iki delikli L tip metal parça",
                "Mechanism": "iğne çevresi",
                "VisualShapeTags": '["iki_delikli","L_tip"]',
            },
        ]

        reranked = rerank_results_by_visual_hints(rows, image_analysis)

        self.assertEqual(reranked[0]["PartCode"], "A")

    def test_diagnosis_terms_map_symptoms_to_searchable_part_families(self) -> None:
        terms = diagnosis_terms_for_query("Makinem ip koparıyor ve dikiş atlıyor")

        self.assertIn("iğne", terms)
        self.assertIn("lüper", terms)
        self.assertIn("tansiyon", terms)

    def test_diagnosis_terms_detect_ip_atlayip_language(self) -> None:
        terms = diagnosis_terms_for_query("Juki MF-7900 ipi atlayıp duruyor tak tak ses yapıyor")

        self.assertIn("iğne", terms)
        self.assertIn("lüper", terms)
        self.assertIn("iplik yolu", terms)
        self.assertIn("rulman", terms)

    def test_machine_group_detection_does_not_match_duzgun_as_duz(self) -> None:
        self.assertIsNone(detect_machine_group_from_text("ip orada sürtüyor ve düzgün akmıyor"))
        self.assertEqual(detect_machine_group_from_text("düz dikiş makinesi"), "Düz")

    def test_sticky_context_extracts_part_code_and_name_from_history(self) -> None:
        sticky = extract_sticky_context_from_history(
            [
                {"role": "user", "text": "Juki MF-7900 makinem var"},
                {"role": "assistant", "text": "Katalogda kontrol edilecek aday parça(lar): 70003363 - İPLİK_GÜZERGÂHI (Sf 4, Ref 32)."},
            ]
        )

        self.assertEqual(sticky["brand"], "JUKI")
        self.assertEqual(sticky["machine_model"], "MF-7900")
        self.assertEqual(sticky["part_code"], "70003363")
        self.assertEqual(sticky["part_name"], "İPLİK_GÜZERGÂHI")
        self.assertEqual(sticky["page_number"], "4")
        self.assertEqual(sticky["ref_no"], "32")
        self.assertEqual(len(sticky["part_candidates"]), 1)
        self.assertEqual(sticky["part_candidates"][0]["code"], "70003363")

    def test_sticky_context_extracts_candidate_part_list_from_history(self) -> None:
        sticky = extract_sticky_context_from_history(
            [
                {
                    "role": "assistant",
                    "text": (
                        "Katalogda kontrol edilecek aday parça(lar): "
                        "40024796 - KAYAR_KAPAK (Sf 4, Ref 13); "
                        "13403407 - KAPAK_DESTEK (Sf 4, Ref 2); "
                        "13402706 - KAPAK_PİMİ (Sf 4, Ref 25)."
                    ),
                }
            ]
        )

        self.assertEqual([item["code"] for item in sticky["part_candidates"]], ["40024796", "13403407", "13402706"])
        self.assertEqual(sticky["part_candidates"][1]["name"], "KAPAK_DESTEK")
        self.assertEqual(sticky["part_candidates"][2]["refNo"], "25")

    def test_select_sticky_candidate_from_ordinal_followup(self) -> None:
        candidates = [
            {"code": "40024796", "name": "KAYAR_KAPAK"},
            {"code": "13403407", "name": "KAPAK_DESTEK"},
            {"code": "13402706", "name": "KAPAK_PİMİ"},
        ]

        self.assertEqual(
            select_sticky_candidate_from_query("ikinci parçanın kodu ne?", candidates)["code"],
            "13403407",
        )
        self.assertEqual(
            select_sticky_candidate_from_query("sonuncusu nerede duruyor?", candidates)["code"],
            "13402706",
        )
        self.assertIsNone(select_sticky_candidate_from_query("ilk hangi kontrolleri yapayım?", candidates))

    def test_sticky_context_does_not_treat_machine_model_as_part_code(self) -> None:
        sticky = extract_sticky_context_from_history(
            [{"role": "assistant", "text": "Bu kayıt JUKI MF-7900-E22/E23 modelinde görünüyor."}]
        )

        self.assertIsNone(sticky["part_code"])

    def test_related_context_terms_expand_slide_cover_assembly(self) -> None:
        terms = related_part_terms_for_context("küçük kayar kapak sürekli yerinden oynuyor")

        self.assertIn("kayar kapak", terms)
        self.assertIn("kapak destek", terms)
        self.assertIn("kapak pimi", terms)

    def test_related_context_terms_prioritize_direct_support_pin_and_hinge(self) -> None:
        self.assertEqual(related_part_terms_for_context("kapağın destek parçası eksik"), ["kapak destek"])
        self.assertEqual(related_part_terms_for_context("ön kapağın pimi kayıp"), ["kapak pimi"])
        terms = related_part_terms_for_context("öndeki kapak menteşeden boşluk yapıyor")
        self.assertEqual(terms[0], "menteşe")

    def test_related_context_terms_keep_family_first_for_which_parts_question(self) -> None:
        terms = related_part_terms_for_context(
            "Kayar kapak oynuyor. Sanki destek parçası eksik. Bu bölümde hangi parçalar değişmeli?"
        )

        self.assertEqual(terms[:3], ["kayar kapak", "kapak destek", "kapak pimi"])

    def test_related_context_terms_expand_thread_guide_family(self) -> None:
        terms = related_part_terms_for_context("sağ taraftaki iplik kılavuzu kırılmış")

        self.assertEqual(terms, ["iplik kılavuzu"])

    def test_term_expansion_includes_catalog_underscore_names_for_slide_cover(self) -> None:
        expanded = expand_part_search_text("kayar kapak")

        self.assertIsNotNone(expanded)
        self.assertIn("KAYAR_KAPAK", expanded or "")
        self.assertIn("SG_SLIDE_COVER", expanded or "")

    def test_related_context_terms_expand_cloth_plate_assembly(self) -> None:
        terms = related_part_terms_for_context("Kumaş geçen plaka tarafında boşluk var")

        self.assertIn("kumaş plaka", terms)
        self.assertIn("arka plaka", terms)
        self.assertIn("plaka blok", terms)

    def test_related_context_terms_expand_front_cover_assembly(self) -> None:
        terms = related_part_terms_for_context("öndeki büyük kapak kapanınca boşluk yapıyor")

        self.assertIn("aşağı açılır kapak", terms)
        self.assertIn("kapak pimi", terms)
        self.assertIn("menteşe", terms)

        context_terms = related_part_terms_for_context("makinenin ön büyük kapağı")
        self.assertIn("aşağı açılır kapak", context_terms)

        hinge_terms = related_part_terms_for_context("öndeki kapak menteşeden boşluk yapıyor")
        self.assertIn("aşağı açılır kapak", hinge_terms)
        self.assertIn("menteşe", hinge_terms)

    def test_related_context_terms_expand_plate_block_direct(self) -> None:
        terms = related_part_terms_for_context("plaka blok aşınmış olabilir")

        self.assertEqual(terms, ["plaka blok"])

    def test_generic_symptom_query_waits_for_machine_context(self) -> None:
        self.assertTrue(
            is_generic_symptom_query_without_context(
                "Makinem ip koparıyor, neden olabilir?",
                extracted_machine_model=None,
                extracted_code=None,
            )
        )
        self.assertFalse(
            is_generic_symptom_query_without_context(
                "Kayar kapak oynuyor, neden olabilir?",
                extracted_machine_model=None,
                extracted_code=None,
            )
        )

    def test_active_context_line_includes_selected_machine(self) -> None:
        line = build_active_context_line(
            {
                "machineBrand": "JUKI",
                "machineModel": "MF-7900",
                "machineVariant": "E22",
                "machineGroup": "Reçme",
            },
            None,
            None,
        )

        self.assertIn("Aktif makine: JUKI MF-7900 E22 | Tip: Reçme", line)

        suffixed_terms = related_part_terms_for_context("makinenin öndeki büyük kapağı")
        self.assertIn("aşağı açılır kapak", suffixed_terms)

    def test_term_expansion_includes_catalog_underscore_names_for_cloth_plate(self) -> None:
        expanded = expand_part_search_text("kumaş plaka")

        self.assertIsNotNone(expanded)
        self.assertIn("PLAKA_MONTAJ", expanded or "")
        self.assertIn("CLOTH_PLATE", expanded or "")
        self.assertIn("CLOTH_PLATE_ASSY", expanded or "")

    def test_term_expansion_includes_catalog_names_for_front_cover(self) -> None:
        expanded = expand_part_search_text("aşağı açılır kapak")

        self.assertIsNotNone(expanded)
        self.assertIn("SALINCAK_KAPAK_E22", expanded or "")
        self.assertIn("SWING_DOWN_COVER_E22", expanded or "")

    def test_context_source_limit_keeps_slide_cover_family_tight(self) -> None:
        sources = [
            {"code": "13403407", "name": "KAPAK_DESTEK", "query": "destek parçası", "similarity": 0.8},
            {"code": "40024796", "name": "KAYAR_KAPAK", "query": "kayar kapak | slide cover", "similarity": 0.74},
            {"code": "13402706", "name": "KAPAK_PİMİ", "query": "pin for cover", "similarity": 0.77},
            {"code": "SM4030655SP", "name": "VİDA", "query": "kayar kapak", "similarity": 0.76},
            {"code": "PS0150042K0", "name": "YAYLI_PİM", "query": "kapak pimi", "similarity": 0.77},
        ]

        limited = limit_context_related_sources(sources, ["kayar kapak", "kapak destek", "kapak pimi", "vida"])

        self.assertEqual([source["code"] for source in limited], ["40024796", "13403407", "13402706"])

    def test_context_source_limit_prioritizes_direct_cover_parts(self) -> None:
        sources = [
            {"code": "40024796", "name": "KAYAR_KAPAK", "query": "kayar kapak", "similarity": 0.75},
            {"code": "13403407", "name": "KAPAK_DESTEK", "query": "destek parçası", "similarity": 0.74},
            {"code": "13402706", "name": "KAPAK_PİMİ", "query": "kapak pimi", "similarity": 0.73},
            {"code": "70001648", "name": "MENTEŞE", "query": "menteşe", "similarity": 0.72},
        ]

        self.assertEqual(
            [source["code"] for source in limit_context_related_sources(sources, ["kapak destek"])],
            ["13403407"],
        )
        self.assertEqual(
            [source["code"] for source in limit_context_related_sources(sources, ["kapak pimi"])],
            ["13402706"],
        )
        self.assertEqual(
            [source["code"] for source in limit_context_related_sources(sources, ["menteşe"])],
            ["70001648"],
        )

    def test_context_source_limit_keeps_cloth_plate_family_tight(self) -> None:
        sources = [
            {"code": "70003355", "name": "PLAKA_MONTAJ", "query": "kumaş plaka | CLOTH_PLATE_ASSY", "similarity": 0.76},
            {"code": "70003357", "name": "PLAKA_ARKA", "query": "arka plaka | CLOTH_PLATE_REAR", "similarity": 0.77},
            {"code": "70003414", "name": "PLAKA", "query": "kumaş plaka | CLOTH_PLATE", "similarity": 0.74},
            {"code": "70003405", "name": "PLAKA_BLOK", "query": "plaka blok | CLOTH_PLATE_BLOCK", "similarity": 0.75},
            {"code": "SM6040450TP", "name": "VİDA M4 L=4", "query": "vida", "similarity": 0.76},
        ]

        limited = limit_context_related_sources(sources, ["kumaş plaka", "arka plaka", "plaka blok", "vida"])

        self.assertEqual([source["code"] for source in limited], ["70003355", "70003357", "70003414", "70003405"])

    def test_context_source_limit_prioritizes_direct_rear_plate(self) -> None:
        sources = [
            {"code": "70003355", "name": "PLAKA_MONTAJ", "query": "kumaş plaka", "similarity": 0.76},
            {"code": "70003357", "name": "PLAKA_ARKA", "query": "arka plaka | CLOTH_PLATE_REAR", "similarity": 0.77},
            {"code": "70003405", "name": "PLAKA_BLOK", "query": "plaka blok", "similarity": 0.75},
        ]

        limited = limit_context_related_sources(sources, ["arka plaka"])

        self.assertEqual([source["code"] for source in limited], ["70003357"])

    def test_context_source_limit_keeps_front_cover_family_tight(self) -> None:
        sources = [
            {"code": "70003402", "name": "SALINCAK_KAPAK_E22", "query": "aşağı açılır kapak | SWING_DOWN_COVER_E22", "similarity": 0.76},
            {"code": "13402706", "name": "KAPAK_PİMİ", "query": "kapak pimi", "similarity": 0.77},
            {"code": "70001648", "name": "MENTEŞE", "query": "menteşe | HINGE", "similarity": 0.75},
            {"code": "23630007", "name": "BORU", "query": "kapak", "similarity": 0.76},
        ]

        limited = limit_context_related_sources(sources, ["aşağı açılır kapak", "kapak pimi", "menteşe", "vida"])

        self.assertEqual([source["code"] for source in limited], ["70003402", "13402706", "70001648"])

    def test_context_source_limit_keeps_plate_block_direct_tight(self) -> None:
        sources = [
            {"code": "70003405", "name": "PLAKA_BLOK", "query": "plaka blok | CLOTH_PLATE_BLOCK", "similarity": 0.77},
            {"code": "70003355", "name": "PLAKA_MONTAJ", "query": "plaka", "similarity": 0.76},
            {"code": "SM6040450TP", "name": "VİDA M4 L=4", "query": "plaka blok", "similarity": 0.76},
        ]

        limited = limit_context_related_sources(sources, ["plaka blok"])

        self.assertEqual([source["code"] for source in limited], ["70003405"])

    def test_context_source_limit_keeps_thread_guide_family_tight(self) -> None:
        sources = [
            {"code": "70003363", "name": "İPLİK_GÜZERGÂHI", "query": "iplik kılavuzu | thread guide", "similarity": 0.77},
            {"code": "PS0150042K0", "name": "YAYLI_PİM", "query": "iplik kılavuzu", "similarity": 0.72},
            {"code": "SM8040412TP", "name": "AYAR_VİDASI M4 L=4", "query": "thread guide", "similarity": 0.71},
            {"code": "13403407", "name": "KAPAK_DESTEK", "query": "guide", "similarity": 0.70},
        ]

        limited = limit_context_related_sources(sources, ["iplik kılavuzu"])

        self.assertEqual([source["code"] for source in limited], ["70003363"])

    def test_term_expansion_keeps_original_and_adds_shop_language(self) -> None:
        expanded = expand_part_search_text("tansiyon yayı")

        self.assertIsNotNone(expanded)
        self.assertIn("tansiyon yayı", expanded or "")
        self.assertIn("ip gergi", expanded or "")
        self.assertIn("gerdirici", expanded or "")

    def test_term_expansion_maps_turkish_thread_guide_to_catalog_language(self) -> None:
        expanded = expand_part_search_text("iplik kılavuzu")

        self.assertIsNotNone(expanded)
        self.assertIn("iplik kılavuzu", expanded or "")
        self.assertIn("thread guide", expanded or "")
        self.assertIn("iplik güzergahı", expanded or "")

    def test_general_knowledge_intents_are_explicitly_guarded(self) -> None:
        self.assertTrue(is_general_knowledge_intent("DIAGNOSE"))
        self.assertTrue(is_general_knowledge_intent("EXPLAIN_PART"))
        self.assertFalse(is_general_knowledge_intent("SEARCH"))

        block = get_intent_mode_block("EXPLAIN_PART")
        self.assertIn("Kesin uyumluluk iddiası verme", block)
        self.assertIn("Katalogdaki kaynak varsa", block)

    def test_diagnose_mode_block_preserves_general_triage_contract(self) -> None:
        block = get_intent_mode_block("DIAGNOSE")

        self.assertIn("Genel usta bilgisi", block)
        self.assertIn("Katalog sonucu varsa", block)
        self.assertIn("netleştirecek", block)

    def test_diagnose_no_result_guidance_gives_general_triage_before_machine_question(self) -> None:
        reply = build_no_result_guidance(
            "Makinem ip koparıyor, neden olabilir?",
            {"intent": "DIAGNOSE"},
            reason="no_result",
        )

        self.assertIn("Genel usta yorumu", reply)
        self.assertIn("makine marka/modelini henüz bilmiyorum", reply.lower())
        self.assertIn("genel kontrol sırası", reply)
        self.assertIn("iğne", reply)
        self.assertIn("lüper", reply)
        self.assertIn("tansiyon", reply)

    def test_generic_diagnosis_waits_for_model_before_catalog_cards(self) -> None:
        should_hold = should_hold_generic_diagnosis_catalog_search(
            intent="DIAGNOSE",
            extracted_machine_model=None,
            extracted_code=None,
            parts=[
                {"part_name": "iğne", "source": "diagnosis_symptom_map"},
                {"part_name": "lüper", "source": "diagnosis_symptom_map"},
            ],
            analysis={"intent": "DIAGNOSE"},
        )

        self.assertTrue(should_hold)

    def test_contextual_diagnosis_can_still_use_catalog_cards(self) -> None:
        should_hold = should_hold_generic_diagnosis_catalog_search(
            intent="DIAGNOSE",
            extracted_machine_model=None,
            extracted_code=None,
            parts=[{"part_name": "iplik kılavuzu", "source": "context_related_part_map"}],
            analysis={"intent": "DIAGNOSE", "context_related_terms": ["iplik kılavuzu"]},
        )

        self.assertFalse(should_hold)

    def test_response_mode_classifier_separates_service_and_customer_formats(self) -> None:
        self.assertEqual(
            classify_response_mode("Bu parça nerede duruyor, nasıl değiştirilir?", "SEARCH", {}),
            "SERVICE_ACTION",
        )
        self.assertEqual(
            classify_response_mode("WhatsApp'tan göndereceğim 5 maddelik mesaj yaz", "DIAGNOSE", {}),
            "CUSTOMER_MESSAGE",
        )
        self.assertEqual(
            classify_response_mode("SM4041055SP fiyatı ne", "SEARCH", {"original_intent": "PRICE"}),
            "PRICE_STOCK",
        )

    def test_price_stock_exact_reply_uses_required_unavailable_phrase(self) -> None:
        reply = build_exact_code_reply_from_sources(
            "SM4041055SP fiyatı ne",
            {
                "intent": "SEARCH",
                "original_intent": "PRICE",
                "unsupported_feature": "fiyat",
                "part_code": "SM4041055SP",
            },
            [
                {
                    "code": "SM4041055SP",
                    "name": "VİDA",
                    "brand": "JUKI",
                    "pageNumber": "4",
                    "refNo": "12",
                }
            ],
        )

        self.assertIsNotNone(reply)
        self.assertIn("fiyat bilgisi bu ekranda henüz aktif değil", reply or "")
        self.assertIn("SM4041055SP", reply or "")

    def test_no_result_guidance_asks_model_without_source_unbacked_examples(self) -> None:
        reply = build_no_result_guidance(
            "Bu parçanın kodunu bul",
            {"intent": "SEARCH"},
            reason="no_result",
        )

        self.assertIn("Makine markası nedir?", reply)
        self.assertIn("Makine modeli nedir?", reply)
        self.assertNotIn("örn:", reply)
        self.assertNotIn("Yamato/Juki", reply)

    def test_no_result_guidance_handles_repair_safety_followup(self) -> None:
        reply = build_no_result_guidance(
            "Tak tak sesi yüzünden makineyi çalıştırmaya devam etsem zarar verir mi? İlk hangi kontrolleri sırayla yapayım?",
            {"intent": "SEARCH"},
            reason="no_result",
        )

        self.assertIn("Güvenlik", reply)
        self.assertIn("çalıştırmaya devam etmeyin", reply)
        self.assertIn("Ustaya bırak", reply)

    def test_no_result_guidance_handles_whatsapp_template_without_history(self) -> None:
        reply = build_no_result_guidance(
            "Bana bunu müşteriye WhatsApp'tan göndereceğim 5 maddelik net mesaj yaz.",
            {"intent": "SEARCH"},
            reason="no_result",
        )

        self.assertIn("1.", reply)
        self.assertIn("2.", reply)
        self.assertIn("3.", reply)
        self.assertIn("ustaya bırak", reply.lower())

    def test_no_result_guidance_handles_whatsapp_template_with_active_part(self) -> None:
        reply = build_no_result_guidance(
            "Bunu müşteriye WhatsApp'tan 5 maddede anlat.",
            {
                "intent": "SEARCH",
                "part_code": "70003363",
                "part_name": "İPLİK_GÜZERGÂHI",
                "current_context": {
                    "pageNumber": "4",
                    "refNo": "32",
                },
            },
            reason="local_guidance",
        )

        self.assertIn("70003363", reply)
        self.assertIn("İPLİK_GÜZERGÂHI", reply)
        self.assertIn("Sf 4", reply)
        self.assertIn("Ref 32", reply)
        self.assertIn("5.", reply)

    def test_active_context_source_keeps_followup_code_grounded(self) -> None:
        source = build_active_context_source(
            {
                "part_code": "70003363",
                "part_name": "İPLİK_GÜZERGÂHI",
                "brand": "JUKI",
                "current_context": {
                    "pageNumber": "4",
                    "refNo": "32",
                    "catalogId": "catalog-1",
                },
            }
        )

        self.assertIsNotNone(source)
        self.assertEqual(source["code"], "70003363")
        self.assertEqual(source["name"], "İPLİK_GÜZERGÂHI")
        self.assertEqual(source["pageNumber"], "4")
        self.assertEqual(source["refNo"], "32")

    def test_active_context_sources_keep_candidate_list_grounded(self) -> None:
        sources = build_active_context_sources(
            {
                "candidate_sources": [
                    {"code": "40024796", "name": "KAYAR_KAPAK", "pageNumber": "4", "refNo": "13"},
                    {"code": "13403407", "name": "KAPAK_DESTEK", "pageNumber": "4", "refNo": "2"},
                    {"code": "13402706", "name": "KAPAK_PİMİ", "pageNumber": "4", "refNo": "25"},
                ],
                "current_context": {},
            }
        )

        self.assertEqual([source["code"] for source in sources], ["40024796", "13403407", "13402706"])
        self.assertEqual(sources[0]["refNo"], "13")

    def test_no_result_guidance_handles_whatsapp_template_with_candidate_list(self) -> None:
        reply = build_no_result_guidance(
            "Bunları müşteriye WhatsApp'tan 5 maddede anlat.",
            {
                "candidate_sources": [
                    {"code": "40024796", "name": "KAYAR_KAPAK", "pageNumber": "4", "refNo": "13"},
                    {"code": "13403407", "name": "KAPAK_DESTEK", "pageNumber": "4", "refNo": "2"},
                ],
                "current_context": {},
            },
            reason="local_guidance",
        )

        self.assertIn("40024796", reply)
        self.assertIn("13403407", reply)
        self.assertIn("Ref 13", reply)
        self.assertIn("Ref 2", reply)

    def test_sanitize_reply_safety_language_removes_overconfident_compatibility_phrase(self) -> None:
        reply = sanitize_reply_safety_language("Bu bilgilerle kesin uyumluluğu daha net görebiliriz.")

        self.assertIn("uyumu", reply)
        self.assertNotIn("kesin uyumlu", reply.lower())

    def test_sanitize_reply_safety_language_removes_source_free_examples(self) -> None:
        reply = sanitize_reply_safety_language(
            "Makinenizin tam marka ve modelini (örneğin JUKI DDL-8700, Yamato AZ8000 gibi) paylaşın."
        )

        self.assertIn("tam marka ve modelini paylaşın", reply)
        self.assertNotIn("DDL-8700", reply)
        self.assertNotIn("AZ8000", reply)

    def test_sanitize_reply_safety_language_removes_mesela_examples(self) -> None:
        reply = sanitize_reply_safety_language(
            "Tam marka ve modelini (mesela Juki DDL-8700, Yamato AZ8000 gibi) paylaş."
        )

        self.assertIn("Tam marka ve modelini paylaş", reply)
        self.assertNotIn("DDL-8700", reply)
        self.assertNotIn("AZ8000", reply)

    def test_sanitize_reply_safety_language_keeps_plate_scope_tight(self) -> None:
        reply = sanitize_reply_safety_language(
            "Bu tür sorunlar plakanın kendisinde veya iğne-lüper zamanlamasında bir ayarsızlık olduğunda yaşanır."
        )

        self.assertIn("plaka sabitlemesinde", reply)
        self.assertNotIn("lüper", reply.lower())

    def test_deterministic_action_reply_uses_safe_catalog_checklist(self) -> None:
        reply = build_deterministic_reply_from_sources(
            "Bu parça nerede duruyor, nasıl değiştirilir?",
            [
                {
                    "code": "70003363",
                    "name": "İPLİK_GÜZERGÂHI",
                    "brand": "JUKI",
                    "pageNumber": "4",
                    "refNo": "32",
                }
            ],
        )

        self.assertIn("70003363", reply)
        self.assertIn("Sf 4", reply)
        self.assertIn("Ref 32", reply)
        self.assertIn("Güvenlik", reply)
        self.assertIn("İplik yolunu fotoğraflayıp eski kılavuzu sök", reply)
        self.assertIn("düşük hızda", reply)
        self.assertIn("Fiziksel konumu uydurmuyorum", reply)
        self.assertNotIn("Güven:", reply)
        self.assertNotIn("Neden:", reply)
        self.assertNotIn("iğneye yakın", reply)

    def test_final_prompt_contains_shared_answer_contract(self) -> None:
        prompt = build_final_prompt(
            history_text="",
            raw_user_query="Kapak oynuyor, ne yapayım?",
            machine_context_line="",
            model_safety_line="",
            active_context_line="",
            relation_context_line="",
            context_page_hint_block="",
            context_text="- Marka: JUKI | Ref No: 13 | Sayfa: 4 | Parça: KAYAR_KAPAK | Kod: 40024796",
            intent="DIAGNOSE",
            general_knowledge_line="",
            intent_mode_block="",
            response_mode_instruction=build_response_mode_instruction("SERVICE_ACTION"),
        )

        self.assertIn("CEVAP STANDARDI", prompt)
        self.assertIn("CEVAP MODU: SERVICE_ACTION", prompt)
        self.assertIn("Teşhis, Parça, Yer, İşlem, Ustaya bırak", prompt)
        self.assertIn("Kısa teşhis", prompt)
        self.assertIn("Katalogda kontrol edilecek parçalar", prompt)
        self.assertIn("Ustaya bırakılacak durum", prompt)
        self.assertIn("Katalogda görünen", prompt)

    def test_diagnosis_filter_drops_broad_semantic_noise(self) -> None:
        rows = [
            {
                "PartCode": "70003363",
                "PartName": "İPLİK_GÜZERGÂHI",
                "similarity": 0.72,
                "fallback": True,
            },
            {
                "PartCode": "BR-001",
                "PartName": "RULMAN",
                "similarity": 0.70,
                "fallback": True,
            },
        ]

        filtered = filter_diagnosis_results(rows, "rulman", "rulman")

        self.assertEqual([r["PartCode"] for r in filtered], ["BR-001"])

    def test_strict_part_lookup_filter_drops_non_looper_results(self) -> None:
        rows = [
            {"PartCode": "70003363", "PartName": "İPLİK_GÜZERGÂHI", "similarity": 0.72},
            {"PartCode": "PS0150042K0", "PartName": "YAYLI_PİM 1.5X 4", "similarity": 0.72},
            {"PartCode": "LP-001", "PartName": "LÜPER", "similarity": 0.70},
        ]

        filtered = filter_strict_part_lookup_results(rows, "lüper", "lüper | looper")

        self.assertEqual([r["PartCode"] for r in filtered], ["LP-001"])

    def test_chat_without_search_routes_to_dynamic_chat_mode(self) -> None:
        is_chat_only, is_general_knowledge_only, response = route_without_search(
            intent="CHAT",
            extracted_part=None,
            extracted_code=None,
            parts=[],
            image_analysis={},
            analysis={"intent": "CHAT"},
            is_general_knowledge_intent=is_general_knowledge_intent,
        )

        self.assertTrue(is_chat_only)
        self.assertFalse(is_general_knowledge_only)
        self.assertIsNone(response)

    def test_source_quality_gate_rejects_out_of_domain_noise(self) -> None:
        response = source_quality_early_response(
            all_sources=[
                {
                    "code": "NOISE-1",
                    "name": "Alakasız Parça",
                    "brand": "JUKI",
                    "similarity": 0.55,
                }
            ],
            intent="SEARCH",
            is_chat_only=False,
            is_general_knowledge_only=False,
            raw_user_query="lüper kodu nedir",
            user_query="lüper kodu nedir",
            analysis={"intent": "SEARCH"},
            extracted_code=None,
            weak_match_min_similarity=0.52,
            build_no_result_guidance=build_no_result_guidance,
            best_similarity=lambda sources: max(float(source.get("similarity") or 0) for source in sources),
            has_lexical_overlap=lambda _query, _sources: False,
            has_domain_part_keyword=lambda _query: False,
            is_general_knowledge_intent=is_general_knowledge_intent,
        )

        self.assertIsNotNone(response)
        self.assertIn("eşleşmedi", response["response"]["answer"])

    def test_source_quality_gate_rejects_low_retrieval_confidence(self) -> None:
        response = source_quality_early_response(
            all_sources=[
                {
                    "code": "LOW-1",
                    "name": "Zayıf Aday",
                    "brand": "YAMATO",
                    "similarity": 0.54,
                }
            ],
            intent="SEARCH",
            is_chat_only=False,
            is_general_knowledge_only=False,
            raw_user_query="model plakası kodu nedir",
            user_query="model plakası kodu nedir",
            analysis={"intent": "SEARCH", "part_name": "model plakası"},
            extracted_code=None,
            weak_match_min_similarity=0.52,
            build_no_result_guidance=build_no_result_guidance,
            best_similarity=lambda sources: max(float(source.get("similarity") or 0) for source in sources),
            has_lexical_overlap=lambda _query, _sources: True,
            has_domain_part_keyword=lambda _query: True,
            is_general_knowledge_intent=is_general_knowledge_intent,
        )

        self.assertIsNotNone(response)
        self.assertIn("parçayı bulamadım", response["response"]["answer"])
        self.assertEqual(response["response"]["debug_intent"]["confidence_gate"], "low")

    def test_confidence_threshold_resolution_prefers_catalog_override(self) -> None:
        thresholds = resolve_confidence_thresholds(
            {
                "resolved_brand": "JUKI",
                "resolved_catalog_ids": ["catalog-1"],
            },
            overrides={
                "brands": {"juki": {"high_confidence": 0.80}},
                "catalogs": {"catalog-1": {"high_confidence": 0.90, "low_confidence": 0.50}},
            },
        )

        self.assertEqual(thresholds.high_confidence, 0.90)
        self.assertEqual(thresholds.low_confidence, 0.50)
        self.assertEqual(thresholds.ambiguity_score_delta, 0.10)
        self.assertEqual(thresholds.source, "catalog:catalog-1")

    def test_search_trace_includes_applied_confidence_thresholds(self) -> None:
        analysis = {
            "confidence_gate": "high",
            "applied_high_threshold": 0.80,
            "applied_low_threshold": 0.50,
            "applied_ambiguity_delta": 0.08,
            "threshold_source": "brand:juki",
        }

        trace = build_search_trace(
            original_query="lüper kodu nedir",
            analysis=analysis,
            search_scope={"resolved_brand": "JUKI"},
            sources=[{"code": "ABC", "name": "Lüper", "similarity": 0.84}],
        )

        self.assertEqual(trace["applied_high_threshold"], 0.80)
        self.assertEqual(trace["applied_low_threshold"], 0.50)
        self.assertEqual(trace["applied_ambiguity_delta"], 0.08)
        self.assertEqual(trace["threshold_source"], "brand:juki")

    def test_source_quality_gate_asks_clarification_for_close_candidates(self) -> None:
        response = source_quality_early_response(
            all_sources=[
                {
                    "code": "4109410",
                    "name": "MODEL PLAKASI",
                    "brand": "YAMATO",
                    "similarity": 0.79,
                },
                {
                    "code": "6209401",
                    "name": "SERİ NUMARA PLAKASI",
                    "brand": "YAMATO",
                    "similarity": 0.76,
                },
            ],
            intent="SEARCH",
            is_chat_only=False,
            is_general_knowledge_only=False,
            raw_user_query="marka plakası kodu nedir",
            user_query="marka plakası kodu nedir",
            analysis={"intent": "SEARCH", "part_name": "marka plakası"},
            extracted_code=None,
            weak_match_min_similarity=0.52,
            build_no_result_guidance=build_no_result_guidance,
            best_similarity=lambda sources: max(float(source.get("similarity") or 0) for source in sources),
            has_lexical_overlap=lambda _query, _sources: True,
            has_domain_part_keyword=lambda _query: True,
            is_general_knowledge_intent=is_general_knowledge_intent,
        )

        self.assertIsNotNone(response)
        self.assertIn("iki yakın parça arasında kaldım", response["response"]["answer"])
        self.assertIn("MODEL PLAKASI", response["response"]["answer"])
        self.assertIn("SERİ NUMARA PLAKASI", response["response"]["answer"])
        self.assertEqual(response["response"]["debug_intent"]["confidence_gate"], "ambiguous")
        self.assertEqual(len(response["response"]["sources"]), 2)

    def test_source_quality_gate_allows_guarded_general_knowledge_without_sources(self) -> None:
        response = source_quality_early_response(
            all_sources=[],
            intent="DIAGNOSE",
            is_chat_only=False,
            is_general_knowledge_only=True,
            raw_user_query="makinem ip koparıyor",
            user_query="makinem ip koparıyor",
            analysis={"intent": "DIAGNOSE"},
            extracted_code=None,
            weak_match_min_similarity=0.52,
            build_no_result_guidance=build_no_result_guidance,
            best_similarity=lambda _sources: None,
            has_lexical_overlap=lambda _query, _sources: False,
            has_domain_part_keyword=lambda _query: False,
            is_general_knowledge_intent=is_general_knowledge_intent,
        )

        self.assertIsNone(response)


if __name__ == "__main__":
    unittest.main()
