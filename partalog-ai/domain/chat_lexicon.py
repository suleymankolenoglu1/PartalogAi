"""Static terminology used by the chat retrieval and diagnosis pipeline."""

DOMAIN_PART_TERMS = {
    "vida", "civata", "somun", "pul", "rondela", "conta", "percin",
    "yay", "plaka", "mil", "rulman", "kayis", "kece", "igne", "disli",
    "kapak", "pim", "burc", "kasnak", "kanca", "yayli", "nozul",
    "kılavuz", "kilavuz", "guzergah", "güzergah", "güzergâh",
}

GENERAL_KNOWLEDGE_INTENTS = {"CHAT", "HELP", "DIAGNOSE", "ADVICE", "EXPLAIN_PART", "PHOTO_GUIDANCE"}

DIAGNOSIS_SEARCH_TERMS: list[tuple[tuple[str, ...], tuple[str, ...]]] = [
    (("ip kop", "iplik kop", "kopuyor", "kopma"), ("iğne", "lüper", "tansiyon", "iplik yolu", "çağanoz")),
    (("atliyor", "atlıyor", "atlayip", "atlayıp", "ip atla", "dikiş at", "dikis at", "atlama"), ("iğne", "lüper", "iplik yolu", "iğne plakası", "baskı ayağı")),
    (("ses", "gürültü", "gurultu", "takirti", "takırtı"), ("rulman", "dişli", "kayış", "kasnak", "mil")),
    (("iğne kır", "igne kir", "iğne kırıyor", "igne kiriyor"), ("iğne", "iğne bağı", "iğne plakası", "baskı ayağı")),
    (("alt ip", "masura", "topluyor", "düğümlen", "dugumlen"), ("mekik", "çağanoz", "masura", "tansiyon")),
    (("kesmiyor", "kesici", "ip kes", "kumaş kes", "kumas kes"), ("bıçak", "alt bıçak", "üst bıçak", "kesici")),
]

CONTEXT_RELATED_PART_TERMS: list[tuple[tuple[str, ...], tuple[str, ...]]] = [
    (("destek parçası", "destek parcasi", "kapak destek", "slide cover support"), ("kapak destek",)),
    (("kapak pimi", "pimi kayıp", "pimi kayip", "pim kayıp", "pim kayip", "pin for cover"), ("kapak pimi",)),
    (("menteşe", "mentese", "hinge"), ("menteşe",)),
    (("arka plaka", "plakasının arka", "plakasinin arka", "cloth plate rear"), ("arka plaka",)),
    (("kayar kapak", "slide cover"), ("kayar kapak", "kapak destek", "kapak pimi", "vida")),
    (("iplik kılavuzu", "iplik kilavuzu", "iplik güzergahı", "iplik guzergahi", "thread guide"), ("iplik kılavuzu",)),
    ((
        "ön kapak",
        "on kapak",
        "front cover",
        "öndeki büyük kapak",
        "ondeki buyuk kapak",
        "öndeki kapak",
        "ondeki kapak",
        "öndeki büyük kapa",
        "ondeki buyuk kapa",
        "ön büyük kapak",
        "on buyuk kapak",
        "ön büyük kapa",
        "on buyuk kapa",
        "aşağı açılan kapak",
        "asagi acilan kapak",
    ), ("aşağı açılır kapak", "kapak pimi", "menteşe", "vida", "rondela")),
    (("kumaş plaka", "kumas plaka", "cloth plate", "plaka taraf"), ("kumaş plaka", "arka plaka", "plaka blok", "vida")),
    (("plaka blok", "cloth plate block"), ("plaka blok",)),
]

TERM_EXPANSIONS: dict[str, list[str]] = {
    "tansiyon": ["ip gergi", "gerdirici", "tansiyon yayı", "iplik tansiyon"],
    "lüper": ["looper", "alt lüper", "üst lüper"],
    "çağanoz": ["mekik", "hook", "bobin yuvası"],
    "iğne": ["igne", "iğne bağı", "iğne barı", "iğne plakası"],
    "iğne plakası": ["igne plakasi", "needle plate", "throat plate"],
    "igne plakasi": ["iğne plakası", "needle plate", "throat plate"],
    "plaka": ["plate"],
    "iplik kılavuzu": ["thread guide", "THREAD_GUIDE", "iplik güzergahı", "iplik güzergâhı"],
    "iplik kilavuzu": ["thread guide", "THREAD_GUIDE", "iplik guzergahi", "iplik güzergahı"],
    "kılavuz": ["guide", "thread guide", "iplik güzergahı"],
    "kilavuz": ["guide", "thread guide", "iplik guzergahi"],
    "kayar kapak": ["KAYAR_KAPAK", "slide cover", "SG_SLIDE_COVER"],
    "kapak destek": ["KAPAK_DESTEK", "slide cover support", "SLIDE_COVER_SUPPORT"],
    "kapak pimi": ["KAPAK_PİMİ", "KAPAK_PIMI", "pin for cover", "PIN FOR COVER"],
    "aşağı açılır kapak": ["SALINCAK_KAPAK_E22", "AŞAĞI_AÇILIR_KAPAK_E22", "ASAGI_ACILIR_KAPAK_E22", "SWING_DOWN_COVER_E22"],
    "menteşe": ["MENTEŞE", "MENTESE", "HINGE"],
    "kumaş plaka": ["PLAKA_MONTAJ", "KUMAŞ_PLAKA", "KUMAS_PLAKA", "CLOTH_PLATE", "CLOTH_PLATE_ASSY"],
    "kumas plaka": ["PLAKA_MONTAJ", "KUMAS_PLAKA", "CLOTH_PLATE", "CLOTH_PLATE_ASSY"],
    "arka plaka": ["PLAKA_ARKA", "CLOTH_PLATE_REAR"],
    "plaka blok": ["PLAKA_BLOK", "CLOTH_PLATE_BLOCK"],
    "baskı": ["baskı ayağı", "presser foot", "ayak"],
    "kayış": ["kasnak kayışı", "belt"],
    "dişli": ["gear", "aktarma dişlisi"],
}

STRICT_PART_LOOKUP_TERMS = {"lüper", "luper", "looper", "çağanoz", "caganoz", "mekik", "rulman", "kayış", "kayis", "dişli", "disli"}

DIAGNOSIS_RESULT_SYNONYMS: dict[str, tuple[str, ...]] = {
    "iğne": ("iğne", "igne", "needle"),
    "lüper": ("lüper", "luper", "looper"),
    "tansiyon": ("tansiyon", "gergi", "gerdirici", "yay", "iplik"),
    "iplik yolu": ("iplik", "güzergah", "guzergah", "kılavuz", "kilavuz", "thread"),
    "çağanoz": ("çağanoz", "caganoz", "mekik", "hook", "bobin"),
    "rulman": ("rulman", "bilya", "bearing"),
    "dişli": ("dişli", "disli", "gear"),
    "kayış": ("kayış", "kayis", "belt"),
    "kasnak": ("kasnak", "pulley"),
    "mil": ("mil", "shaft"),
    "baskı ayağı": ("baskı", "baski", "ayak", "presser"),
    "iğne plakası": ("iğne", "igne", "plaka", "plate"),
    "mekik": ("mekik", "çağanoz", "caganoz", "hook", "bobin"),
    "masura": ("masura", "bobin"),
    "bıçak": ("bıçak", "bicak", "kesici", "knife"),
    "alt bıçak": ("bıçak", "bicak", "kesici", "knife"),
    "üst bıçak": ("bıçak", "bicak", "kesici", "knife"),
    "kesici": ("bıçak", "bicak", "kesici", "knife"),
}
