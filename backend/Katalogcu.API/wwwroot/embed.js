(function () {
  if (window.PartalogEmbed) return;

  var SCRIPT_NS = "partalog-embed";
  var CLIENT_SOURCE = "partalog-embed-client";

  function trimSlash(value) {
    return String(value || "").replace(/\/+$/, "");
  }

  function normalizeApiBaseUrl(value) {
    var trimmed = trimSlash(value || "");
    if (/\/api$/i.test(trimmed)) {
      return trimmed.replace(/\/api$/i, "");
    }
    return trimmed;
  }

  function deriveAppBaseUrl(apiBaseUrl) {
    return normalizeApiBaseUrl(apiBaseUrl || "");
  }

  function resolveScript() {
    if (document.currentScript) return document.currentScript;
    var scripts = document.getElementsByTagName("script");
    return scripts[scripts.length - 1] || null;
  }

  function readConfig(options) {
    var script = resolveScript();
    var ds = (script && script.dataset) || {};
    var scriptSrc = (script && script.src) || "";
    var scriptOrigin = scriptSrc ? new URL(scriptSrc).origin : "";

    var config = Object.assign(
      {
        token: ds.publicToken || ds.token || "",
        storeSlug: ds.store || ds.storeSlug || "",
        apiBaseUrl: normalizeApiBaseUrl(ds.apiBaseUrl || scriptOrigin),
        appBaseUrl: trimSlash(ds.appBaseUrl || ""),
        target: ds.target || "",
        scriptElement: script,
        height: ds.height || "780px",
        autoMount: (ds.autoMount || "true").toLowerCase() !== "false",
        iframeClass: ds.iframeClass || "",
        analytics: (ds.analytics || "true").toLowerCase() !== "false"
      },
      options || {}
    );

    if (!config.token && !config.storeSlug) {
      throw new Error("data-public-token veya data-store zorunlu.");
    }

    return config;
  }

  function findHost(target) {
    if (typeof target === "string") {
      return document.getElementById(target) || document.querySelector(target);
    }
    return target && target.nodeType === 1 ? target : null;
  }

  function resolveHost(config) {
    var host = findHost(config.target);
    if (host) return host;

    var script = config && config.scriptElement;
    if (script && script.parentElement) {
      return script.parentElement;
    }

    return null;
  }

  function verifyOrigin(config) {
    var endpoint = trimSlash(config.apiBaseUrl) + "/api/embed/verify-origin";
    return fetch(endpoint, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        publicToken: config.token,
        storeSlug: config.storeSlug,
        origin: window.location.origin
      }),
      credentials: "omit"
    }).then(function (res) {
      if (!res.ok) throw new Error("verify-origin başarısız: " + res.status);
      return res.json();
    }).then(function (payload) {
      if (!payload || payload.allowed !== true) {
        var verifyError = new Error("Bu domain embed için yetkili değil.");
        verifyError.code = payload && payload.reason ? payload.reason : "origin_not_allowed";
        verifyError.payload = payload || null;
        throw verifyError;
      }
      return payload;
    });
  }

  function resolveAppBaseUrl(config, verifyPayload) {
    if (config.appBaseUrl) {
      return trimSlash(config.appBaseUrl);
    }

    if (verifyPayload && verifyPayload.appBaseUrl) {
      return trimSlash(verifyPayload.appBaseUrl);
    }

    var derived = deriveAppBaseUrl(config.apiBaseUrl);
    console.warn("[PartalogEmbed] app base url verify-origin cevabinda gelmedi, api base url'den türetildi:", derived || "(same-origin)");
    return trimSlash(derived);
  }

  function buildIframeUrl(config, verifyPayload) {
    var base = resolveAppBaseUrl(config, verifyPayload);
    var effectiveToken = (verifyPayload && verifyPayload.publicToken) || config.token;
    var path = "/p/" + encodeURIComponent(effectiveToken);
    var params = new URLSearchParams();
    params.set("embed", "1");
    if (verifyPayload && verifyPayload.theme) params.set("theme", verifyPayload.theme);
    if (verifyPayload && verifyPayload.mode) params.set("mode", verifyPayload.mode);
    return base + path + "?" + params.toString();
  }

  function appendPoweredByBadge(host) {
    var wrap = document.createElement("div");
    wrap.style.display = "flex";
    wrap.style.justifyContent = "flex-end";
    wrap.style.marginTop = "8px";

    var badge = document.createElement("a");
    badge.href = "https://partalog.tech";
    badge.target = "_blank";
    badge.rel = "noopener noreferrer";
    badge.textContent = "Powered by Partalog";
    badge.style.display = "inline-flex";
    badge.style.alignItems = "center";
    badge.style.minHeight = "24px";
    badge.style.padding = "0 9px";
    badge.style.borderRadius = "999px";
    badge.style.border = "1px solid rgba(148, 163, 184, 0.35)";
    badge.style.background = "rgba(248, 250, 252, 0.9)";
    badge.style.color = "#475569";
    badge.style.font = "600 11px/1.2 Inter, system-ui, sans-serif";
    badge.style.textDecoration = "none";
    badge.style.letterSpacing = "0.01em";

    wrap.appendChild(badge);
    host.appendChild(wrap);
  }

  function getFriendlyErrorCopy(err) {
    var code = err && err.code ? String(err.code) : "";
    switch (code) {
      case "plan_upgrade_required":
        return {
          title: "Plan yukseltme gerekiyor",
          text: "Bu isletmenin paketi web sitesi entegrasyonunu desteklemiyor. Embed kullanimi icin Plan 2 veya ustu gerekir."
        };
      case "origin_not_allowed":
        return {
          title: "Bu domain yetkili degil",
          text: "Bu site adresi embed icin izinli degil. Panelden domain ekleyip allowlist'e almalisin."
        };
      case "invalid_token":
        return {
          title: "Gecersiz public link",
          text: "Kullanilan public token gecersiz veya iptal edilmis. Panelden yeni bir public link uretip tekrar dene."
        };
      case "token_required":
      case "token_or_store_required":
        return {
          title: "Embed kimligi eksik",
          text: "Embed kodunda data-public-token veya data-store alani eksik. Panelden guncel embed kodunu tekrar kopyala."
        };
      case "invalid_store":
        return {
          title: "Magaza kodu gecersiz",
          text: "Kullanilan data-store degeri taninmadi. Paneldeki magaza kodunu kontrol edip tekrar kopyala."
        };
      case "origin_required":
        return {
          title: "Domain bilgisi okunamadi",
          text: "Tarayici mevcut site adresini dogrulayamadi. Domain adresini ve embed kurulumunu kontrol et."
        };
      case "owner_not_found":
        return {
          title: "Magaza bulunamadi",
          text: "Bu embed kaydi bir isletme ile eslesmedi. Public link ve embed ayarlarini kontrol et."
        };
      default:
        return {
          title: "Embed yuklenemedi",
          text: "Partalog vitrinini yuklerken bir sorun olustu. Domain, public link ve API adresini kontrol et."
        };
    }
  }

  function renderErrorState(host, err) {
    if (!host) return;
    var copy = getFriendlyErrorCopy(err || {});
    host.innerHTML = "";

    var card = document.createElement("div");
    card.style.width = "100%";
    card.style.minHeight = "220px";
    card.style.boxSizing = "border-box";
    card.style.padding = "24px";
    card.style.border = "1px solid rgba(203, 213, 225, 0.9)";
    card.style.borderRadius = "18px";
    card.style.background = "linear-gradient(180deg, rgba(248,250,252,0.98) 0%, rgba(241,245,249,0.98) 100%)";
    card.style.boxShadow = "0 10px 28px rgba(15, 23, 42, 0.08)";
    card.style.display = "grid";
    card.style.alignContent = "center";
    card.style.gap = "12px";
    card.style.fontFamily = "Inter, system-ui, sans-serif";

    var badge = document.createElement("div");
    badge.textContent = "Partalog Embed";
    badge.style.width = "fit-content";
    badge.style.minHeight = "28px";
    badge.style.padding = "0 10px";
    badge.style.borderRadius = "999px";
    badge.style.display = "inline-flex";
    badge.style.alignItems = "center";
    badge.style.border = "1px solid rgba(148,163,184,0.35)";
    badge.style.background = "rgba(255,255,255,0.9)";
    badge.style.color = "#1d4ed8";
    badge.style.fontSize = "11px";
    badge.style.fontWeight = "700";
    badge.style.letterSpacing = "0.02em";

    var title = document.createElement("h3");
    title.textContent = copy.title;
    title.style.margin = "0";
    title.style.color = "#0f172a";
    title.style.fontSize = "22px";
    title.style.lineHeight = "1.25";

    var text = document.createElement("p");
    text.textContent = copy.text;
    text.style.margin = "0";
    text.style.color = "#475569";
    text.style.fontSize = "14px";
    text.style.lineHeight = "1.6";
    text.style.maxWidth = "560px";

    var note = document.createElement("p");
    note.textContent = "Sorun devam ederse panelde Web Sitesine Ekle ekranindan domain ve script ayarlarini tekrar kontrol et.";
    note.style.margin = "2px 0 0";
    note.style.color = "#64748b";
    note.style.fontSize = "12px";
    note.style.lineHeight = "1.5";

    card.appendChild(badge);
    card.appendChild(title);
    card.appendChild(text);
    card.appendChild(note);
    host.appendChild(card);
  }

  function sendEventToApi(config, detail) {
    if (!config.analytics) return;
    if (!config.apiBaseUrl || !config.token) return;
    if (!detail || detail.name === 'embed:resize') return;

    var endpoint = trimSlash(config.apiBaseUrl) + "/api/embed/events?token=" + encodeURIComponent(config.token);
    var body = {
      eventName: detail.name,
      source: "sdk-v1",
      pageUrl: window.location.href,
      payload: detail.payload || {}
    };

    try {
      if (navigator.sendBeacon) {
        var blob = new Blob([JSON.stringify(body)], { type: "application/json" });
        navigator.sendBeacon(endpoint, blob);
        return;
      }
    } catch (_) {
      // fallback fetch below
    }

    fetch(endpoint, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
      keepalive: true,
      credentials: "omit"
    }).catch(function () {
      // telemetry path should not break host site flow
    });
  }

  function forwardEvents(iframe, config) {
    function applyResizeHeight(height) {
      var numeric = Number(height || 0);
      if (!numeric || !isFinite(numeric)) return;
      iframe.style.height = Math.max(320, Math.round(numeric)) + 'px';
    }

    function onMessage(event) {
      if (!iframe || event.source !== iframe.contentWindow) return;
      var data = event.data || {};
      if (data.source !== CLIENT_SOURCE || !data.event) return;

      var detail = {
        name: data.event,
        payload: data.payload || {},
        timestamp: data.timestamp || new Date().toISOString()
      };

      if (data.event === 'embed:resize') {
        applyResizeHeight(detail.payload && detail.payload.height);
        return;
      }

      document.dispatchEvent(new CustomEvent(data.event, { detail: detail }));
      document.dispatchEvent(new CustomEvent(SCRIPT_NS + ":event", { detail: detail }));
      sendEventToApi(config, detail);

      if (typeof config.onEvent === "function") {
        config.onEvent(detail);
      }
    }

    window.addEventListener("message", onMessage);
    return function cleanup() {
      window.removeEventListener("message", onMessage);
    };
  }

  function mount(options) {
    var config = readConfig(options);
    var host = resolveHost(config);
    if (!host) {
      throw new Error("Embed mount hedefi bulunamadı.");
    }

    return verifyOrigin(config).then(function (verifyPayload) {
      if (!config.token && verifyPayload && verifyPayload.publicToken) {
        config.token = verifyPayload.publicToken;
      }
      host.innerHTML = "";

      var iframe = document.createElement("iframe");
      iframe.src = buildIframeUrl(config, verifyPayload);
      iframe.title = "Partalog Catalog Embed";
      iframe.loading = "lazy";
      iframe.allow = "clipboard-read; clipboard-write";
      iframe.style.width = "100%";
      iframe.style.height = config.height;
      iframe.style.border = "0";
      iframe.style.display = "block";
      iframe.style.background = "transparent";
      if (config.iframeClass) iframe.className = config.iframeClass;

      var cleanup = forwardEvents(iframe, config);
      host.appendChild(iframe);
      if (verifyPayload && verifyPayload.whiteLabel !== true) {
        appendPoweredByBadge(host);
      }

      return {
        iframe: iframe,
        destroy: function () {
          cleanup();
          iframe.remove();
        }
      };
    }).catch(function (err) {
      renderErrorState(host, err);
      throw err;
    });
  }

  function init(options) {
    return mount(options).catch(function (err) {
      console.error("[PartalogEmbed]", err);
      throw err;
    });
  }

  window.PartalogEmbed = {
    init: init,
    mount: mount
  };

  try {
    var config = readConfig();
    if (config.autoMount) {
      init(config);
    }
  } catch (err) {
    console.error("[PartalogEmbed]", err);
  }
})();
