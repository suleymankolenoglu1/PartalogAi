"""Response-mode classification and deterministic chat replies."""

import re

from services.chat_intent import unavailable_feature_notice
from services.chat_memory import (
    detect_machine_model_from_text,
    format_active_part_context,
    format_source_identity,
)
from services.chat_sources import build_source_confidence, build_source_match_reason
from services.chat_terms import (
    diagnosis_terms_for_query,
    normalize_for_overlap,
)


def is_generic_part_name(name: str | None) -> bool:
    if not name:
        return False
    n = (name or "").strip().lower()
    generic = {
        "vida", "civata", "somun", "pul", "conta", "perçin", "percin",
        "yay", "plaka", "mil", "rulman", "kapak", "pim", "burç", "burc",
        "dişli", "disli",
    }
    return n in generic


def should_answer_with_local_guidance(user_query: str) -> bool:
    normalized_query = normalize_for_overlap(user_query or "")
    return any(
        token in normalized_query
        for token in (
            "whatsapp",
            "musteriye mesaj",
            "müşteriye mesaj",
            "5 maddelik",
            "kontrol listesi",
            "tak tak",
            "zarar",
            "calistirmaya devam",
            "çalıştırmaya devam",
            "kontrol sirasi",
            "kontrol sırası",
        )
    )


def asks_for_location_or_replacement(user_query: str) -> bool:
    normalized = normalize_for_overlap(user_query or "")
    hints = (
        "nerede", "yeri", "yerinde", "duruyor", "nasil degistirilir", "nasıl değiştirilir",
        "degistirilir", "değiştirilir", "degismeli", "değişmeli", "kontrol", "takilir",
        "takılır", "ne yapmaliyim", "ne yapmalıyım",
    )
    return any(hint in normalized for hint in hints)


def build_replacement_steps_for_sources(sources: list[dict]) -> str:
    combined = normalize_for_overlap(
        " ".join(
            str(source.get("name") or source.get("description") or source.get("query") or "")
            for source in sources[:3]
        )
    )

    if any(token in combined for token in ("iplik", "kilavuz", "guzergah", "güzergah", "guide")):
        return (
            "Değişim sırası: İplik yolunu fotoğraflayıp eski kılavuzu sök, aynı kodlu parçayı aynı yöne bakacak şekilde tak, "
            "ipliğin keskin kenara sürtmediğini elle çekerek kontrol et, sonra düşük hızda dene."
        )

    if any(token in combined for token in ("kayar kapak", "slide cover", "kapak destek", "cover support", "kapak pimi")):
        return (
            "Değişim sırası: Kapağı yerinden çıkar, kapak desteği/pim/vidalarda eksik veya aşınma var mı kontrol et, "
            "bozuk parçayı aynı ref/kodla değiştir, kapağın boşluk yapmadan kaydığını elle dene."
        )

    if any(token in combined for token in ("plaka", "plate", "cloth", "throat")):
        return (
            "Değişim sırası: Plakayı tutan vidaları gevşetip plakayı kaldır, altındaki destek ve oturma yüzeyini temizle, "
            "eğik/aşınmış parçayı değiştirip plakayı boşluk bırakmadan sabitle."
        )

    if any(token in combined for token in ("vida", "screw", "pim", "pin", "somun", "nut", "pul", "washer")):
        return (
            "Değişim sırası: Eksik veya gevşek bağlantı elemanını aynı ölçü/kodla değiştir, diş sıyırmamak için fazla sıkma, "
            "parçayı elle hareket ettirip boşluk kalmadığını kontrol et."
        )

    if any(token in combined for token in ("mentese", "menteşe", "hinge")):
        return (
            "Değişim sırası: Menteşe bağlantısını ve pimini kontrol et, aşınmış menteşeyi aynı kodla değiştir, "
            "kapak açılıp kapanırken kasma veya boşluk kalmadığını dene."
        )

    return (
        "Değişim sırası: Eski parçayı ve bağlantı vidası/pimini sök, aynı kodlu parçayı aynı yuvaya oturt, "
        "sürtme/boşluk kalmadığını elle çevirerek kontrol et, sonra düşük hızda dene."
    )


def classify_response_mode(user_query: str, intent: str | None, analysis: dict) -> str:
    normalized_query = normalize_for_overlap(user_query or "")
    normalized_intent = str(intent or "").upper()
    original_intent = str((analysis or {}).get("original_intent") or "").upper()

    if any(token in normalized_query for token in ("whatsapp", "musteriye mesaj", "müşteriye mesaj", "5 maddelik")):
        return "CUSTOMER_MESSAGE"
    if any(token in normalized_query for token in ("tak tak", "zarar", "calistirmaya devam", "çalıştırmaya devam", "kontrol sirasi", "kontrol sırası")):
        return "SAFETY_CHECKLIST"
    if normalized_intent in {"PRICE", "STOCK"} or original_intent in {"PRICE", "STOCK"}:
        return "PRICE_STOCK"
    if asks_for_location_or_replacement(user_query):
        return "SERVICE_ACTION"
    if normalized_intent == "DIAGNOSE":
        return "DIAGNOSIS_TRIAGE"
    if normalized_intent in {"SEARCH", "COMPATIBILITY", "COMPARE", "EXPLAIN_PART"}:
        return "PART_LOOKUP"
    return "GENERAL"


def build_no_result_guidance(user_query: str, analysis: dict, reason: str) -> str:
    normalized_query = normalize_for_overlap(user_query or "")
    if any(token in normalized_query for token in ("whatsapp", "musteriye mesaj", "müşteriye mesaj", "5 maddelik", "kontrol listesi")):
        active_part = format_active_part_context(analysis)
        part_line = (
            f"2. Kontrol edilen parça: {active_part}. Kod/sayfa/ref bilgisini müşteriye böyle aktarabilirsiniz.\n"
            if active_part
            else "2. İğne, iplik yolu ve görünen kapak/plaka bölgelerinde kırık, sürtme veya gevşeklik var mı kontrol edin.\n"
        )
        return (
            "1. Güvenlik için makineyi durdurup elektriğini kesin.\n"
            f"{part_line}"
            "3. Vuruntu, tak tak sesi veya sürtme devam ediyorsa makineyi çalıştırmayın.\n"
            "4. Parçanın kodu, ref numarası veya net fotoğrafı varsa paylaşın; katalogdan doğru adayı teyit edelim.\n"
            "5. Lüper/iğne hizası, zamanlama veya gövdeye temas varsa işlemi ustaya bırakın."
        )

    if any(token in normalized_query for token in ("tak tak", "zarar", "calistirmaya devam", "çalıştırmaya devam", "kontrol sirasi", "kontrol sırası")):
        return (
            "Güvenlik: Tak tak sesi veya sürtme varsa makineyi çalıştırmaya devam etmeyin; küçük temas büyüyüp iğne, lüper veya kapak/plaka grubuna zarar verebilir.\n"
            "1. Makineyi kapatıp elektriği kesin.\n"
            "2. İğnenin eğik/kırık olmadığını ve doğru oturduğunu kontrol edin.\n"
            "3. İplik yolunda kılavuz, tansiyon ve kapak çevresinde sürtme/kırık var mı bakın.\n"
            "4. Lüper/iğne bölgesini elle yavaş çevirerek temas veya vuruntu var mı dinleyin.\n"
            "Ustaya bırak: Vuruntu lüper-iğne bölgesinden geliyorsa, hizalama gerekiyorsa veya parça gövdeye temas ediyorsa çalıştırmadan ustaya bırakın."
        )

    intent = str((analysis or {}).get("intent") or "").upper()
    if intent == "DIAGNOSE":
        diagnosis_terms = diagnosis_terms_for_query(user_query)
        controls = ", ".join(diagnosis_terms[:5]) if diagnosis_terms else "iğne, lüper, tansiyon, çağanoz/masura ve baskı ayağı"
        return (
            "Genel usta yorumu: Makine marka/modelini henüz bilmiyorum; o yüzden kesin parça söylemem doğru olmaz. "
            "Ama belirti üzerinden genel kontrol sırası verebilirim.\n"
            f"İlk bakılacak noktalar: {controls}.\n"
            "Netleştirmek için bana tam makine marka-modelini, makine tipini ve mümkünse sorunlu bölgenin yakın görüntüsünü gönder; "
            "sonra katalogdaki doğru parçaya daraltırım."
        )

    if intent == "ADVICE":
        return (
            "Ustam, katalogda bu soruya doğrudan bağlanan net bir parça bulamadım. "
            "Genel öneri verebilirim ama kesin seçim için marka-model, ölçü veya parça kodu lazım. "
            "Makine tipi ve kullanım yerini yazarsan öneriyi katalog sonuçlarıyla daraltırım."
        )

    if intent == "EXPLAIN_PART":
        part_name = (analysis or {}).get("part_name") or "bu parça"
        return (
            f"Ustam, katalogda '{part_name}' için net kaynak yakalayamadım; bu yüzden kod veya uyumluluk uydurmayacağım. "
            "Parçanın görevini genel olarak anlatabilirim, ama kesin katalog eşleşmesi için parça kodu, ref no, sayfa veya fotoğraf paylaşman gerekir."
        )

    if intent == "PHOTO_GUIDANCE":
        return (
            "Parçayı daha net bulmam için fotoğrafı düz ve aydınlık zeminde çek ustam. "
            "Varsa üzerindeki kod/marka yazısını yakın plan, parçanın yan profilini ve makine üzerindeki konumunu ayrı ayrı göster. "
            "Bunlar gelirse görsel ipuçlarını katalog aramasıyla daha iyi eşleştiririm."
        )

    brand = (analysis or {}).get("brand")
    part_code = (analysis or {}).get("part_code")
    part_name = (analysis or {}).get("part_name")
    dimensions = (analysis or {}).get("dimensions")
    machine_group = (analysis or {}).get("machine_group")
    machine_model = (analysis or {}).get("machine_model")
    context_part = (analysis or {}).get("context_part")
    if not machine_model and context_part:
        maybe_model, _ = detect_machine_model_from_text(str(context_part))
        if maybe_model:
            machine_model = maybe_model

    if reason == "out_of_domain":
        intro = "Ustam, bu sorgu katalogdaki parça içeriğiyle net eşleşmedi."
    elif reason == "weak_match":
        intro = "Ustam, eşleşmeler zayıf kaldı; yanlış parça önermemek için durdurdum."
    elif reason == "retrieval_low_confidence":
        intro = "Ustam, parçayı bulamadım; biraz daha detaylandırır mısınız?"
    elif reason == "low_confidence":
        intro = "Ustam, ne aradığını büyük ölçüde anladım ama hâlâ belirsizlik var."
    elif machine_model and part_name:
        label = " ".join(x for x in [brand, machine_model] if x).strip() or machine_model
        intro = f"Ustam, {label} bağlamında '{part_name}' için katalogda net kod bulamadım."
    else:
        intro = "Ustam, veritabanında bu sorguya doğrudan bir sonuç bulamadım."

    questions: list[str] = []

    if not brand and not machine_model:
        questions.append("Makine markası nedir?")
    if not machine_model:
        questions.append("Makine modeli nedir? Model etiketindeki tam adı yazabilir misin?")
    if not machine_group and not machine_model:
        questions.append("Makine tipi nedir?")

    if not part_code:
        if machine_model and part_name:
            questions.append("Eski parçanın üzerindeki kod/ref no veya ilgili katalog sayfasını paylaşır mısın?")
        elif not dimensions and is_generic_part_name(part_name):
            questions.append("Parçanın ölçüsü nedir? Çap, uzunluk veya diş ölçüsünü yazabilir misin?")
        else:
            questions.append("Parça kodu varsa birebir yazar mısın?")

    if part_code and not dimensions:
        questions.append("Kod doğruysa, ölçü/model bilgisini de paylaşır mısın?")

    if not questions:
        questions.append("Parça kodu veya net ölçü paylaşırsan nokta atışı bulurum.")

    q_text = "\n".join([f"- {q}" for q in questions[:3]])
    return f"{intro}\nDoğru parçayı netleyelim:\n{q_text}"


def build_deterministic_reply_from_sources(user_query: str, sources: list[dict]) -> str:
    if not sources:
        return (
            "Ustam, şu an kısa özet modundayım. "
            "Sonuç listesi boş görünüyor; parça kodu veya ölçü paylaşırsan net arama yaparım."
        )

    picks = sources[:3]
    if asks_for_location_or_replacement(user_query):
        listed = "; ".join(format_source_identity(source) for source in picks)
        extra = len(sources) - len(picks)
        extra_text = f" Ayrıca {extra} aday daha var." if extra > 0 else ""
        return (
            f"Katalogda kontrol edilecek aday parça(lar): {listed}.{extra_text}\n"
            "Güvenlik: Makinenin elektriğini kesmeden kapak, plaka, kılavuz veya hareketli parçaya müdahale etme.\n"
            "Yeri: Fiziksel konumu uydurmuyorum; katalog şemasında yukarıdaki Sayfa/Ref numarasından takip et.\n"
            f"{build_replacement_steps_for_sources(picks)}\n"
            "Ustaya bırak: Parça gövdeye temas ediyorsa, vuruntu sesi varsa veya hassas hizalama gerekiyorsa makineyi çalıştırmadan ustaya bırak."
        )

    item_chunks = []
    for source in picks:
        code = source.get("code") or "-"
        name = source.get("name") or "Parça"
        brand = source.get("brand") or ""
        page = str(source.get("pageNumber") or "").strip()
        ref_no = str(source.get("refNo") or "").strip()
        source_bits = []
        if page:
            source_bits.append(f"Sf {page}")
        if ref_no:
            source_bits.append(f"Ref {ref_no}")
        source_text = f" - {' / '.join(source_bits)}" if source_bits else ""
        reason = source.get("matchReason") or build_source_match_reason(source)
        confidence = source.get("confidenceLabel") or build_source_confidence(source)[0]
        reason_text = f"; Neden: {reason}" if reason else ""
        confidence_text = f"; Güven: {confidence}" if confidence else ""
        if brand:
            item_chunks.append(f"{code} ({name}, {brand}{source_text}{confidence_text}{reason_text})")
        else:
            item_chunks.append(f"{code} ({name}{source_text}{confidence_text}{reason_text})")

    listed = ", ".join(item_chunks)
    extra = len(sources) - len(picks)
    extra_text = f" Ayrıca {extra} sonuç daha var." if extra > 0 else ""

    return (
        f"Ustam, katalogda eşleşen parçaları buldum: {listed}.{extra_text} "
        "En doğru sonucu seçmek için sayfa/ref bilgisini kontrol edebilirsin; gerekiyorsa marka, model veya ölçüyle daraltırım."
    )


def sanitize_reply_safety_language(reply: str | None) -> str:
    text = (reply or "").strip()
    if not text:
        return text
    replacements = {
        "kesin uyumluluğu": "uyumu",
        "kesin uyumluluğunu": "uyumunu",
        "kesin uyumluluk": "uyum",
        "kesin uyumlu": "uyumlu",
        "iğne-lüper zamanlamasında bir ayarsızlık": "plaka sabitlemesinde bir gevşeklik",
        "igne-luper zamanlamasinda bir ayarsizlik": "plaka sabitlemesinde bir gevşeklik",
        "iğne-lüper zamanlaması": "plaka sabitlemesi",
        "igne-luper zamanlamasi": "plaka sabitlemesi",
    }
    sanitized = text
    for needle, replacement in replacements.items():
        sanitized = re.sub(needle, replacement, sanitized, flags=re.IGNORECASE)
    sanitized = re.sub(r"\s*\(örneğin[^)]*\)", "", sanitized, flags=re.IGNORECASE)
    sanitized = re.sub(r"\s*\(ornegin[^)]*\)", "", sanitized, flags=re.IGNORECASE)
    sanitized = re.sub(r"\s*\(mesela[^)]*\)", "", sanitized, flags=re.IGNORECASE)
    sanitized = re.sub(r"\s*\(mekik değil[^)]*\)", "", sanitized, flags=re.IGNORECASE)
    sanitized = re.sub(r"\s*\(mekik degil[^)]*\)", "", sanitized, flags=re.IGNORECASE)
    return sanitized


def normalize_lookup_token(value: str | None) -> str:
    return re.sub(r"[^A-Z0-9]+", "", str(value or "").upper())


def extract_lookup_tokens(user_query: str, analysis: dict) -> list[str]:
    tokens: list[str] = []

    def add_token(raw: str | None) -> None:
        normalized = normalize_lookup_token(raw)
        if normalized and any(ch.isdigit() for ch in normalized) and normalized not in tokens:
            tokens.append(normalized)

    add_token(analysis.get("part_code"))
    for part in analysis.get("parts") or []:
        if isinstance(part, dict):
            add_token(part.get("part_code"))

    for match in re.findall(r"[A-Za-z0-9-]{3,}", user_query or ""):
        add_token(match)

    return tokens


def has_compatibility_hint(user_query: str, analysis: dict) -> bool:
    text = (user_query or "").lower()
    hints = [
        "hangi model",
        "hangi makine",
        "uyumlu",
        "uyar mi",
        "uyar mı",
        "hangi cihaz",
        "hangi seri",
    ]
    intent = str(analysis.get("intent") or "").upper()
    return intent == "COMPATIBILITY" or any(h in text for h in hints)


def build_exact_code_reply_from_sources(user_query: str, analysis: dict, sources: list[dict]) -> str | None:
    if not sources:
        return None

    lookup_tokens = extract_lookup_tokens(user_query, analysis)
    if not lookup_tokens:
        return None

    exact_sources = [
        source for source in sources
        if normalize_lookup_token(source.get("code")) in lookup_tokens
        or normalize_lookup_token(source.get("refNo")) in lookup_tokens
    ]

    if not exact_sources:
        return None

    primary = exact_sources[0]
    code = primary.get("code") or "-"
    name = primary.get("name") or "Parça"
    reason = primary.get("matchReason") or build_source_match_reason(primary)
    confidence = primary.get("confidenceLabel") or build_source_confidence(primary)[0]
    reason_text = f" Neden aday: {reason}." if reason else ""
    confidence_text = f" Güven: {confidence}." if confidence else ""
    page_numbers = []
    model_rows = []

    for source in exact_sources:
        page = str(source.get("pageNumber") or "").strip()
        if page and page not in page_numbers:
            page_numbers.append(page)

        brand = str(source.get("brand") or "").strip()
        model = str(source.get("machine_model") or "").strip()
        if brand and model:
            label = f"{brand} {model}"
        else:
            label = brand or model
        if label and label not in model_rows:
            model_rows.append(label)

    page_text = f" Kaynak: {', '.join([f'Sf {p}' for p in page_numbers[:3]])}." if page_numbers else ""
    unavailable_notice = unavailable_feature_notice(analysis)

    if has_compatibility_hint(user_query, analysis):
        if model_rows:
            extra = f" Ayrıca {len(model_rows) - 4} model daha var." if len(model_rows) > 4 else ""
            return (
                f"Ustam, {code} kodlu {name} katalogda şu model/makine bağlamlarında geçiyor: "
                f"{', '.join(model_rows[:4])}.{extra}{page_text}{confidence_text}{reason_text}{unavailable_notice}"
            )
        return f"Ustam, {code} kodlu {name} bulundu ama bu kayıtta model bilgisi boş görünüyor.{page_text}{confidence_text}{reason_text}{unavailable_notice}"

    return f"Ustam, {code} kodlu parça katalogda {name} olarak geçiyor.{page_text}{confidence_text}{reason_text}{unavailable_notice}"
