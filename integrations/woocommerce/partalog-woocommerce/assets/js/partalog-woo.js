(function () {
  if (window.PartalogWooRuntimeLoaded) return;
  window.PartalogWooRuntimeLoaded = true;

  function fillTemplate(template, payload) {
    if (!template) return "";
    return String(template).replace(/\{(\w+)\}/g, function (_, key) {
      var raw = payload && payload[key] != null ? payload[key] : "";
      return encodeURIComponent(String(raw));
    });
  }

  function requestJson(url, method, body) {
    return fetch(url, {
      method: method || "POST",
      headers: { "Content-Type": "application/json" },
      credentials: "include",
      body: JSON.stringify(body || {})
    })
      .then(function (response) {
        return response.json().catch(function () {
          return {};
        }).then(function (data) {
          if (!response.ok) {
            return {
              success: false,
              message: data && data.message ? data.message : "WooCommerce istegi basarisiz."
            };
          }

          return data;
        });
      })
      .catch(function (error) {
        return {
          success: false,
          message: error && error.message ? error.message : "Istek gonderilemedi."
        };
      });
  }

  var config = window.PartalogWooEmbedConfig || {};
  var hostConfig = window.PartalogHostConfig || {};

  if ((config.mode === "woocommerce_cart" || config.mode === "woocommerce_availability_cart") && config.addToCartUrl) {
    hostConfig.onAddToCart = function (item) {
      return requestJson(config.addToCartUrl, "POST", {
        catalogId: item.catalogId || null,
        pageNumber: item.pageNumber || null,
        catalogItemId: item.catalogItemId || null,
        partCode: item.partCode || null,
        partName: item.partName || null,
        quantity: item.quantity || 1
      });
    };
  }

  if (config.enableAvailability && config.availabilityUrl) {
    hostConfig.onAvailability = function (payload) {
      return requestJson(config.availabilityUrl, "POST", payload);
    };
  }

  if (config.mode === "search_redirect" && config.searchUrlTemplate && !hostConfig.onSearch) {
    hostConfig.onSearch = function (item) {
      window.location.assign(fillTemplate(config.searchUrlTemplate, item || {}));
      return { success: true, message: "WooCommerce arama sayfasina yonlendiriliyor." };
    };
  }

  if (config.mode === "product_redirect" && config.productUrlTemplate && !hostConfig.onViewProduct) {
    hostConfig.onViewProduct = function (item) {
      window.location.assign(fillTemplate(config.productUrlTemplate, item || {}));
      return { success: true, message: "WooCommerce urun sayfasina yonlendiriliyor." };
    };
  }

  window.PartalogHostConfig = hostConfig;
})();
