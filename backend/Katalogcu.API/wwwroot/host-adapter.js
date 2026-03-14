(function () {
  if (window.PartalogHostAdapter) return;

  var EMBED_SOURCE = "partalog-embed-client";
  var ADAPTER_SOURCE = "partalog-host-adapter";

  function resolveScript() {
    if (document.currentScript) return document.currentScript;
    var scripts = document.getElementsByTagName("script");
    return scripts[scripts.length - 1] || null;
  }

  function trim(value) {
    return String(value || "").trim();
  }

  function parseHeaders(raw) {
    if (!raw) return {};
    try {
      var parsed = JSON.parse(raw);
      return parsed && typeof parsed === "object" ? parsed : {};
    } catch (_) {
      return {};
    }
  }

  function getGlobalConfig() {
    var cfg = window.PartalogHostConfig;
    return cfg && typeof cfg === "object" ? cfg : {};
  }

  function resolveHandler(candidate) {
    return typeof candidate === "function" ? candidate : null;
  }

  function readConfig(options) {
    var script = resolveScript();
    var ds = (script && script.dataset) || {};
    var globalConfig = getGlobalConfig();
    var cartConfig = globalConfig.cart && typeof globalConfig.cart === "object" ? globalConfig.cart : {};

    return Object.assign(
      {
        availabilityUrl: trim(ds.availabilityUrl || globalConfig.availabilityUrl),
        addToCartUrl: trim(ds.addToCartUrl || cartConfig.url || globalConfig.addToCartUrl),
        addToCartMethod: trim(ds.addToCartMethod || cartConfig.method || globalConfig.addToCartMethod || "POST").toUpperCase(),
        productUrlTemplate: trim(ds.productUrlTemplate || globalConfig.productUrlTemplate),
        searchUrlTemplate: trim(ds.searchUrlTemplate || globalConfig.searchUrlTemplate),
        allowedEmbedOrigin: trim(ds.allowedEmbedOrigin || globalConfig.allowedEmbedOrigin),
        headers: Object.assign({}, parseHeaders(ds.headers), globalConfig.headers || {}),
        credentials: ds.credentials || globalConfig.credentials || "include",
        onAvailability: resolveHandler(globalConfig.onAvailability),
        onAddToCart: resolveHandler(globalConfig.onAddToCart || cartConfig.handler),
        onViewProduct: resolveHandler(globalConfig.onViewProduct),
        onSearch: resolveHandler(globalConfig.onSearch),
        mapCartItem: resolveHandler(cartConfig.map)
      },
      options || {}
    );
  }

  function postMessageToFrame(targetWindow, eventName, payload) {
    if (!targetWindow || typeof targetWindow.postMessage !== "function") return;
    targetWindow.postMessage(
      {
        source: ADAPTER_SOURCE,
        event: eventName,
        payload: payload || {},
        timestamp: new Date().toISOString()
      },
      "*"
    );
  }

  function dispatchHostEvent(eventName, payload) {
    if (typeof document === "undefined" || typeof CustomEvent === "undefined") return;
    document.dispatchEvent(
      new CustomEvent(eventName, {
        detail: payload || {}
      })
    );
  }

  function isAllowedOrigin(config, eventOrigin) {
    if (!config.allowedEmbedOrigin) return true;
    return trim(eventOrigin) === config.allowedEmbedOrigin;
  }

  async function requestJson(url, method, body, config) {
    var requestUrl = url;
    var fetchOptions = {
      method: method || "POST",
      headers: Object.assign({ "Content-Type": "application/json" }, config.headers || {}),
      credentials: config.credentials === "omit" ? "omit" : "include"
    };

    if (String(fetchOptions.method).toUpperCase() !== "GET") {
      fetchOptions.body = JSON.stringify(body || {});
    } else if (body && typeof body === "object") {
      var targetUrl = new URL(url, window.location.origin);
      Object.keys(body).forEach(function (key) {
        var value = body[key];
        if (value == null) return;
        if (typeof value === "object") {
          targetUrl.searchParams.set(key, JSON.stringify(value));
          return;
        }

        targetUrl.searchParams.set(key, String(value));
      });
      requestUrl = targetUrl.toString();
    }

    var response;
    try {
      response = await fetch(requestUrl, fetchOptions);
    } catch (error) {
      return {
        success: false,
        message: error && error.message ? error.message : "Host istegi gonderilemedi."
      };
    }

    var data = null;

    try {
      data = await response.json();
    } catch (_) {
      data = null;
    }

    if (!response.ok) {
      return {
        success: false,
        message: (data && data.message) || response.statusText || "Request failed"
      };
    }

    return data || { success: true };
  }

  function normalizeAvailabilityPayload(payload) {
    return {
      catalogId: payload && payload.catalogId ? payload.catalogId : null,
      pageNumber: payload && payload.pageNumber ? payload.pageNumber : null,
      items: Array.isArray(payload && payload.items) ? payload.items : []
    };
  }

  function normalizeAddToCartPayload(payload) {
    var item = payload && payload.item ? payload.item : payload;
    return {
      catalogId: payload && payload.catalogId ? payload.catalogId : null,
      pageNumber: payload && payload.pageNumber ? payload.pageNumber : null,
      partCode: item && item.partCode ? item.partCode : null,
      partName: item && item.partName ? item.partName : null,
      quantity: item && item.quantity ? item.quantity : 1,
      catalogItemId: item && item.catalogItemId ? item.catalogItemId : null,
      productId: item && item.productId ? item.productId : null
    };
  }

  function normalizeViewPayload(payload) {
    return {
      catalogId: payload && payload.catalogId ? payload.catalogId : null,
      pageNumber: payload && payload.pageNumber ? payload.pageNumber : null,
      partCode: payload && payload.partCode ? payload.partCode : null,
      partName: payload && payload.partName ? payload.partName : null,
      productId: payload && payload.productId ? payload.productId : null,
      catalogItemId: payload && payload.catalogItemId ? payload.catalogItemId : null,
      quantity: payload && payload.quantity ? payload.quantity : 1
    };
  }

  function fillTemplate(template, payload) {
    if (!template) return "";
    return String(template).replace(/\{(\w+)\}/g, function (_, key) {
      var raw = payload && payload[key] != null ? payload[key] : "";
      return encodeURIComponent(String(raw));
    });
  }

  function redirectTo(url) {
    if (!url) return false;
    window.location.assign(url);
    return true;
  }

  function normalizeCartRequest(request, config) {
    if (typeof config.mapCartItem === "function") {
      var mapped = config.mapCartItem(request);
      return mapped && typeof mapped === "object" ? mapped : request;
    }

    return request;
  }

  async function handleAvailability(config, payload) {
    var request = normalizeAvailabilityPayload(payload);

    if (typeof config.onAvailability === "function") {
      return (await Promise.resolve(config.onAvailability(request))) || { items: [] };
    }

    if (!config.availabilityUrl) {
      return { items: [] };
    }

    return requestJson(config.availabilityUrl, "POST", request, config);
  }

  async function handleAddToCart(config, payload) {
    var request = normalizeAddToCartPayload(payload);

    if (typeof config.onAddToCart === "function") {
      var handlerResult = await Promise.resolve(config.onAddToCart(request));
      return handlerResult || { success: true, message: "Host sepet islemi tetiklendi." };
    }

    if (config.addToCartUrl) {
      return requestJson(
        config.addToCartUrl,
        config.addToCartMethod || "POST",
        normalizeCartRequest(request, config),
        config
      );
    }

    var productRedirect = fillTemplate(config.productUrlTemplate, request);
    if (productRedirect) {
      redirectTo(productRedirect);
      return { success: true, message: "Urun sayfasina yonlendiriliyor." };
    }

    var searchRedirect = fillTemplate(config.searchUrlTemplate, request);
    if (searchRedirect) {
      redirectTo(searchRedirect);
      return { success: true, message: "Site ici arama sonucuna yonlendiriliyor." };
    }

    return {
      success: false,
      message: "Host site icin mevcut bir sepet veya yonlendirme aksiyonu tanimlanmamis."
    };
  }

  async function handleViewProduct(config, payload) {
    var request = normalizeViewPayload(payload);

    if (typeof config.onViewProduct === "function") {
      var handlerResult = await Promise.resolve(config.onViewProduct(request));
      return handlerResult || { success: true, message: "Urun aksiyonu tetiklendi." };
    }

    var productRedirect = fillTemplate(config.productUrlTemplate, request);
    if (productRedirect) {
      redirectTo(productRedirect);
      return { success: true, message: "Urun sayfasina yonlendiriliyor." };
    }

    var searchRedirect = fillTemplate(config.searchUrlTemplate, request);
    if (searchRedirect) {
      redirectTo(searchRedirect);
      return { success: true, message: "Site ici arama sonucuna yonlendiriliyor." };
    }

    return {
      success: false,
      message: "Urun detay aksiyonu icin bir URL sablonu veya handler tanimlanmamis."
    };
  }

  async function handleSearch(config, payload) {
    var request = normalizeViewPayload(payload);

    if (typeof config.onSearch === "function") {
      var handlerResult = await Promise.resolve(config.onSearch(request));
      return handlerResult || { success: true, message: "Arama aksiyonu tetiklendi." };
    }

    var searchRedirect = fillTemplate(config.searchUrlTemplate, request);
    if (searchRedirect) {
      redirectTo(searchRedirect);
      return { success: true, message: "Site ici arama sonucuna yonlendiriliyor." };
    }

    var productRedirect = fillTemplate(config.productUrlTemplate, request);
    if (productRedirect) {
      redirectTo(productRedirect);
      return { success: true, message: "Urun sayfasina yonlendiriliyor." };
    }

    return {
      success: false,
      message: "Arama aksiyonu icin bir URL sablonu veya handler tanimlanmamis."
    };
  }

  function init(options) {
    var config = readConfig(options);

    async function onMessage(event) {
      var data = event.data || {};
      if (data.source !== EMBED_SOURCE || !data.event) return;
      if (!isAllowedOrigin(config, event.origin)) return;

      dispatchHostEvent("partalog:adapter:event", {
        event: data.event,
        origin: event.origin || "",
        payload: data.payload || {}
      });

      if (data.event === "part:availability-request") {
        var availabilityResult;
        try {
          availabilityResult = await handleAvailability(config, data.payload);
        } catch (error) {
          availabilityResult = {
            success: false,
            message: error && error.message ? error.message : "Availability istegi basarisiz."
          };
        }
        dispatchHostEvent("partalog:availability-result", availabilityResult);
        postMessageToFrame(event.source, "part:availability-result", availabilityResult);
        return;
      }

      if (data.event === "part:add-to-cart") {
        dispatchHostEvent("partalog:cart:add-request", normalizeAddToCartPayload(data.payload));
        var addToCartResult;
        try {
          addToCartResult = await handleAddToCart(config, data.payload);
        } catch (error) {
          addToCartResult = {
            success: false,
            message: error && error.message ? error.message : "Host sepet istegi basarisiz."
          };
        }
        dispatchHostEvent("partalog:cart:add-result", addToCartResult);
        postMessageToFrame(event.source, "cart:add-result", addToCartResult);
        return;
      }

      if (data.event === "part:view-product") {
        var viewProductResult;
        try {
          viewProductResult = await handleViewProduct(config, data.payload);
        } catch (error) {
          viewProductResult = {
            success: false,
            message: error && error.message ? error.message : "Urun aksiyonu basarisiz."
          };
        }
        dispatchHostEvent("partalog:part:action-result", viewProductResult);
        postMessageToFrame(event.source, "part:action-result", viewProductResult);
        return;
      }

      if (data.event === "part:search") {
        var searchResult;
        try {
          searchResult = await handleSearch(config, data.payload);
        } catch (error) {
          searchResult = {
            success: false,
            message: error && error.message ? error.message : "Arama aksiyonu basarisiz."
          };
        }
        dispatchHostEvent("partalog:part:action-result", searchResult);
        postMessageToFrame(event.source, "part:action-result", searchResult);
      }
    }

    window.addEventListener("message", onMessage);

    return {
      destroy: function () {
        window.removeEventListener("message", onMessage);
      }
    };
  }

  window.PartalogHostAdapter = {
    init: init
  };

  var script = resolveScript();
  if (script && ((script.dataset || {}).autoMount || "true").toLowerCase() !== "false") {
    init();
  }
})();
