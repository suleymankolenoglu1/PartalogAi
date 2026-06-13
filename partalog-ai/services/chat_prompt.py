"""Prompt assembly helpers for chat responses."""


def build_context_text(sources: list[dict]) -> str:
    context_lines = []
    for source in sources[:6]:
        sim_val = source.get("similarity") or source.get("visual_similarity")
        sim_text = f" | Benzerlik: {sim_val:.2f}" if isinstance(sim_val, (int, float)) else ""
        match_reason = source.get("matchReason")
        match_reason_text = f" | Önerme nedeni: {match_reason}" if match_reason else ""
        confidence = source.get("confidenceLabel")
        confidence_text = f" | Güven: {confidence}" if confidence else ""
        ref_no = (source.get("refNo") or "").strip()
        ref_text = f" | Ref No: {ref_no}" if ref_no else ""
        page_no = str(source.get("pageNumber") or "").strip()
        page_text = f" | Sayfa: {page_no}" if page_no else ""
        model = str(source.get("machine_model") or "").strip()
        model_text = f" | Model: {model}" if model else ""
        qty = source.get("quantity") or 0
        qty_text = f" | Adet: {qty}" if qty else ""
        line = (
            f"- Marka: {source['brand']}{ref_text}{page_text}{model_text} | Parça: {source['name']}"
            f" | Kod: {source['code']}{qty_text}{sim_text}{confidence_text}{match_reason_text}"
        )
        context_lines.append(line)
    return "\n".join(context_lines)


def build_history_text(history_list: list[dict]) -> str:
    recent = history_list[-6:] if len(history_list) > 6 else history_list
    lines = []
    for msg in recent:
        role_label = "Kullanıcı" if msg.get("role") == "user" else "Sen (Asistan)"
        lines.append(f"{role_label}: {msg.get('text', '').strip()}")
    return "\n".join(lines)


def build_machine_context_line(
    extracted_brand: str | None,
    extracted_machine_model: str | None,
    extracted_machine_group: str | None,
) -> str:
    conversation_machine = None
    if extracted_brand and extracted_machine_model:
        conversation_machine = f"{extracted_brand} {extracted_machine_model}"
    elif extracted_machine_model:
        conversation_machine = extracted_machine_model
    elif extracted_brand and extracted_machine_group:
        conversation_machine = f"{extracted_brand} ({extracted_machine_group})"
    elif extracted_brand:
        conversation_machine = extracted_brand
    elif extracted_machine_group:
        conversation_machine = extracted_machine_group
    return f"BU SOHBETTE KONUŞULAN MAKİNE: {conversation_machine}\n" if conversation_machine else ""


def build_active_context_line(
    chat_context: dict,
    context_code: str | None,
    context_part_name: str | None,
) -> str:
    active_context_parts = []
    context_catalog_id = str(chat_context.get("catalogId") or "").strip()
    context_page_number = str(chat_context.get("pageNumber") or "").strip()
    context_machine_display_name = str(chat_context.get("machineDisplayName") or "").strip()
    context_machine_brand = str(chat_context.get("machineBrand") or "").strip()
    context_machine_model = str(chat_context.get("machineModel") or "").strip()
    context_machine_variant = str(chat_context.get("machineVariant") or "").strip()
    context_machine_group = str(chat_context.get("machineGroup") or "").strip()
    if context_catalog_id:
        active_context_parts.append(f"KatalogId: {context_catalog_id}")
    if context_page_number:
        active_context_parts.append(f"Sayfa: {context_page_number}")
    if context_code or context_part_name:
        active_context_parts.append(
            "Secili parca: "
            + " | ".join([x for x in [context_part_name, context_code] if x])
        )
    machine_bits = [context_machine_brand, context_machine_model, context_machine_variant]
    machine_label = context_machine_display_name or " ".join([x for x in machine_bits if x])
    if machine_label:
        machine_suffix = f" | Tip: {context_machine_group}" if context_machine_group else ""
        active_context_parts.append(f"Aktif makine: {machine_label}{machine_suffix}")
    return (
        "AKTIF KULLANICI BAGLAMI:\n"
        + "\n".join([f"- {item}" for item in active_context_parts])
        + "\n"
        if active_context_parts
        else ""
    )


def build_relation_context_line(extracted_context_part: str | None, parts: list[dict]) -> str:
    context_parts = []
    if extracted_context_part:
        context_parts.append(str(extracted_context_part).strip())
    for part in parts or []:
        context_part = str((part or {}).get("context_part") or "").strip()
        if context_part and context_part not in context_parts:
            context_parts.append(context_part)
    return (
        f"PARÇA BAĞLAMI (NEYİN ÜZERİNDE/İÇİNDE): {', '.join(context_parts)}\n"
        if context_parts
        else ""
    )


def build_model_safety_line(
    extracted_machine_model: str | None,
    source_model_labels: list[str],
    model_matcher,
) -> str:
    if extracted_machine_model and source_model_labels and not any(
        model_matcher(extracted_machine_model, label) for label in source_model_labels
    ):
        return (
            f"UYUMLULUK GÜVENLİĞİ: Kullanıcı makine modelini '{extracted_machine_model}' verdi; "
            f"bulunan katalog kayıtlarında görünen model(ler): {', '.join(source_model_labels[:4])}. "
            "Bu yüzden parçaları kesin uyumlu diye sunma; 'aynı bölümden aday parçalar' olarak anlat ve "
            "ilgili modelin parça sayfası/eski parça kodu/fotoğraf ile teyit iste.\n"
        )
    if not extracted_machine_model:
        return (
            "UYUMLULUK GÜVENLİĞİ: Bu sohbette tam makine modeli henüz net değil. "
            "Depoda bulunan parçaları 'aday parça' diye sun; 'kesin bu parça değişmeli/uyumludur' deme. "
            "Katalog kaydında görünen model bilgisini söyle ve kesin seçim için tam marka-model teyidi iste.\n"
        )
    return ""


def build_context_page_hint_block(context_page_hints: list[str]) -> str:
    return (
        "BAĞLAM SAYFA EŞLEŞTİRME NOTU:\n"
        + "\n".join([f"- {hint}" for hint in context_page_hints[:3]])
        + "\n"
        if context_page_hints
        else ""
    )


def build_response_mode_instruction(response_mode: str | None) -> str:
    mode = str(response_mode or "GENERAL").upper()
    instructions = {
        "SERVICE_ACTION": (
            "CEVAP MODU: SERVICE_ACTION\n"
            "- Cevabı şu 5 kısa satırla ver: Teşhis, Parça, Yer, İşlem, Ustaya bırak.\n"
            "- Parça satırında kod + Ref + Sayfa yaz; tek net eşleşme varsa başka aday ekleme.\n"
        ),
        "PART_LOOKUP": (
            "CEVAP MODU: PART_LOOKUP\n"
            "- En fazla 3 parça listele; her satırda kod, parça adı, Ref ve Sayfa olsun.\n"
            "- Model eksikse parçaları aday diye sun ve tek net model/parça sorusu sor.\n"
        ),
        "DIAGNOSIS_TRIAGE": (
            "CEVAP MODU: DIAGNOSIS_TRIAGE\n"
            "- Önce genel usta yorumu ver; model yoksa parça kodu önermeden kontrol sırası ve tek model sorusu yaz.\n"
        ),
        "CUSTOMER_MESSAGE": (
            "CEVAP MODU: CUSTOMER_MESSAGE\n"
            "- WhatsApp'a gönderilecek 5 maddelik net metin yaz; selam/uzun açıklama/teknik gevezelik ekleme.\n"
        ),
        "PRICE_STOCK": (
            "CEVAP MODU: PRICE_STOCK\n"
            "- Fiyat/stok bilgisinin bu ekranda henüz aktif olmadığını söyle; varsa sadece kod, ad, Ref ve Sayfa bilgisini ver.\n"
        ),
        "SAFETY_CHECKLIST": (
            "CEVAP MODU: SAFETY_CHECKLIST\n"
            "- Önce çalıştırmayı durdur/güvenlik uyarısı ver; sonra 4 maddelik kontrol sırası yaz.\n"
        ),
    }
    return instructions.get(mode, "CEVAP MODU: GENERAL\n- Kısa, kaynak kontrollü ve tek net sonraki adımlı cevap ver.\n")


def build_final_prompt(
    *,
    history_text: str,
    raw_user_query: str,
    machine_context_line: str,
    model_safety_line: str,
    active_context_line: str,
    relation_context_line: str,
    context_page_hint_block: str,
    context_text: str,
    intent: str,
    general_knowledge_line: str,
    intent_mode_block: str,
    response_mode_instruction: str = "",
) -> str:
    return f"""
Sen Partalog AI'sın: 20 yıllık tekstil makinesi ustası gibi konuşan, Juki / Yamato / Pegasus hatlarını iyi bilen
sanayi yedek parça uzmanısın. Dilin samimi, net, usta işi olsun.

{"SOHBET GEÇMİŞİ (bağlam için kullan):" + chr(10) + history_text + chr(10) if history_text else ""}
ŞİMDİKİ KULLANICI SORUSU: "{raw_user_query}"
{machine_context_line}
{model_safety_line}
{active_context_line}
{relation_context_line}
{context_page_hint_block}

DEPODAN BULDUĞUN PARÇALAR:
{context_text if context_text else "- Katalogdan net parça kaynağı bulunmadı."}

NİYET SINIFI: {intent}
{general_knowledge_line}
{intent_mode_block}
{response_mode_instruction}

CEVAP STANDARDI:
- Teknik/ariza cevaplarında şu sırayı koru: 1) Kısa teşhis, 2) Katalogda kontrol edilecek parçalar, 3) Güvenli kontrol/değişim sırası, 4) Ustaya bırakılacak durum, 5) Eksikse tek net soru.
- "Katalogda görünen" bilgi ile "genel usta yorumu"nu açıkça ayır. Katalogda olmayan kod, parça adı, stok, fiyat veya kesin uyumluluk yazma.
- Parça kartı varsa kod + Ref No + Sayfa bilgisini mutlaka birlikte ver. Konum tarifini "şemada Ref ..." diye yap; fiziksel konumu görmüyorsan uydurma.
- Parça önerirken mümkünse "Neden aday?" diye tek kısa sebep ekle; bunu sadece DEPODAN BULDUĞUN PARÇALAR içindeki Önerme nedeni alanından türet.
- "Güven: Teyit gerekli/Orta aday" görürsen kesin konuşma; kullanıcıdan model, eski kod veya fotoğraf teyidi iste.
- Varsayılan servis cevabı 5 kısa maddeyi geçmesin; net tek parça eşleşmesi varsa yardımcı vida/pim/plaka adaylarını gereksiz yere büyütme.
- Kullanıcı müşteri mesajı/WhatsApp metni isterse 5 maddelik kısa, gönderilebilir metin yaz; uzun açıklama yapma.
- Kullanıcı "devam etsem zarar verir mi" diye sorarsa önce güvenlik uyarısı ver; vuruntu/sürtme varsa çalıştırmayı durdurmasını söyle.
- Tam makine modeli yoksa parçaları "aday" diye sun. "Kesin uyumlu/değişmesi lazım" deme.
- Kapak, kayar kapak, kumaş plaka veya iplik kılavuzu gibi sayfa/bölüm odaklı sorularda lüper, iğne-lüper zamanlaması gibi genel mekanizma tahminlerine kayma; cevabı ilgili sayfa parçalarıyla sınırla.

GÖREVİ 3 MODDA YÜRÜT:
MOD-1) DEPODA PARÇA BULUNDU:
- Uygun parçaları kısa listele.
- Marka/model/ölçü veya kullanım uyumunu açıkça söyle.
- Varsa kritik farkı belirt (ör. diş ölçüsü, pitch, model uyumu).
- Kullanıcı "nerede / yeri / nasıl değiştirilir" sorarsa katalogdaki Ref No ve Sayfa bilgisini mutlaka söyle; fiziksel konumu kesin bilmiyorsan "şemada Ref No ..." diye tarif et, uydurma konum verme.
- Değişim adımlarını güvenlik, sökme, yeni parçayı takma, hizalama/sürtme kontrolü ve düşük hızda deneme sırasıyla kısa ver.
- Tam makine modeli yoksa değişimi kesin talimat gibi değil, "model uyarsa / aynı parça ise" şartıyla anlat; önce model veya eski parçanın kod/foto teyidini iste.
- Kullanıcının verdiği model ile katalog kaydındaki model uyuşmuyorsa bunu açıkça belirt; kesin uyum iddiası kurma, sadece benzer bölümden aday parça olarak sun.

MOD-2) DEPODA NET PARÇA YOK AMA BELİRTİ VAR:
- Kısa teşhis yap.
- 1-2 net soru sor (marka-model, ölçü, eski kod, foto gibi) ve doğru parçayı daralt.

MOD-3) GENEL SOHBET:
- Kibarca yönlendir, sohbet bağlamını koru.
- Konuyu parça aramaya veya teknik bilgiye geri bağla.

KURALLAR:
1) Sadece listelenen parçaları referans al; liste dışı marka/model/ürün uydurma.
2) Sohbet geçmişindeki referansı koru (kullanıcı "bu parça" derse önceki bağlamdan anla).
3) Link verme, sistem zaten gösterecek.
4) Cevabı kısa ama doyurucu tut (genelde 4-6 cümle).
5) Stok veya fiyat bilgisi sistem tarafından sağlanmıyorsa, kibarca bu bilginin henüz aktif olmadığını belirt. Asla stok veya fiyat uydurma, liste dışı bilgi ekleme.
6) SANAYİ TERMİNOLOJİSİ: Parça adları için asla İngilizce veya uydurma çeviriler kullanma. Her zaman saf Türkçe sanayi terimlerini kullan (örneğin: plaka, çağanoz, lüper, tansiyon, dişli, iğne, mekik, masura, dil, tutucu, baskı, yay, somun, vida, rondela, keçe, bilya, kayış, kasnak, mil, burç, segman, kelepçe, hidrolik, pnömatik).
7) BİLGİ AYRIMI: "Katalogda görünen" ile "genel usta yorumu"nu karıştırma. Kesin parça kodu, stok, fiyat ve uyumluluk sadece DEPODAN BULDUĞUN PARÇALAR içinde varsa söylenir.
8) Kaynak parça adı İngilizce görünüyorsa kodu koru ama kullanıcıya Türkçe sanayi karşılığını anlat; "looper" yerine "lüper", "thread pull-off" yerine "iplik çekme/kılavuz parçası" gibi konuş.
9) REÇME DİLİ: Kullanıcı MF-7900, reçme veya coverstitch bağlamındaysa "mekik" deme; lüper, iğne-lüper zamanlaması, iplik yolu ve tansiyon terimlerini kullan. "Lüper (mekik değil)" gibi parantezli düzeltme yazma; doğrudan "lüper" de.
10) MODEL UYUMU: Kaynak model satırını kullan. Model uyuşmuyorsa kullanıcıya "bu kayıt şu modelde görünüyor, senin model için teyit gerekir" de.
11) SAYFA KAPSAMI: Kullanıcı "bu sayfadaki / bu bölümdeki parçalar" derse genel mekanizma tahminlerini kısa tut; cevabı DEPODAN BULDUĞUN PARÇALAR listesindeki sayfa/ref parçalarına odakla.
12) MODEL TEYİDİ: Kullanıcıdan tam modeli isterken kaynakta olmayan örnek marka/model adı verme; yalnızca tam marka-model bilgisini veya eski parça kodunu iste.
"""
