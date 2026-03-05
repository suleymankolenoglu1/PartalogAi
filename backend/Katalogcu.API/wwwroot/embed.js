(function () {
  if (window.PartalogEmbed) return;

  var SCRIPT_NS = "partalog-embed";
  var CLIENT_SOURCE = "partalog-embed-client";

  function trimSlash(value) {
    return String(value || "").replace(/\/+$/, "");
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
        apiBaseUrl: trimSlash(ds.apiBaseUrl || scriptOrigin),
        appBaseUrl: trimSlash(ds.appBaseUrl || ""),
        target: ds.target || "partalog-embed-root",
        height: ds.height || "780px",
        autoMount: (ds.autoMount || "true").toLowerCase() !== "false",
        iframeClass: ds.iframeClass || "",
        analytics: (ds.analytics || "true").toLowerCase() !== "false"
      },
      options || {}
    );

    if (!config.appBaseUrl) {
      throw new Error("data-app-base-url zorunlu (örn: https://app.partalog.tech).");
    }

    if (!config.token) {
      throw new Error("data-public-token zorunlu.");
    }

    return config;
  }

  function findHost(target) {
    if (typeof target === "string") {
      return document.getElementById(target) || document.querySelector(target);
    }
    return target && target.nodeType === 1 ? target : null;
  }

  function verifyOrigin(config) {
    var endpoint = trimSlash(config.apiBaseUrl) + "/api/embed/verify-origin";
    return fetch(endpoint, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        publicToken: config.token,
        origin: window.location.origin
      }),
      credentials: "omit"
    }).then(function (res) {
      if (!res.ok) throw new Error("verify-origin başarısız: " + res.status);
      return res.json();
    }).then(function (payload) {
      if (!payload || payload.allowed !== true) {
        throw new Error("Bu domain embed için yetkili değil.");
      }
      return payload;
    });
  }

  function buildIframeUrl(config, verifyPayload) {
    var base = trimSlash(config.appBaseUrl);
    var path = "/p/" + encodeURIComponent(config.token);
    var params = new URLSearchParams();
    params.set("embed", "1");
    if (verifyPayload && verifyPayload.theme) params.set("theme", verifyPayload.theme);
    if (verifyPayload && verifyPayload.mode) params.set("mode", verifyPayload.mode);
    return base + path + "?" + params.toString();
  }

  function sendEventToApi(config, detail) {
    if (!config.analytics) return;
    if (!config.apiBaseUrl || !config.token) return;

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
    function onMessage(event) {
      if (!iframe || event.source !== iframe.contentWindow) return;
      var data = event.data || {};
      if (data.source !== CLIENT_SOURCE || !data.event) return;

      var detail = {
        name: data.event,
        payload: data.payload || {},
        timestamp: data.timestamp || new Date().toISOString()
      };

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
    var host = findHost(config.target);
    if (!host) throw new Error("Embed mount hedefi bulunamadı: " + config.target);

    return verifyOrigin(config).then(function (verifyPayload) {
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

      return {
        iframe: iframe,
        destroy: function () {
          cleanup();
          iframe.remove();
        }
      };
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
