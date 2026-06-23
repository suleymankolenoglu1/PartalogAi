

export const environment = {
  production: false,
  apiUrl: 'http://localhost:5159/api',
  features: {
    enableChatbot: true,
    enableCatalogAnalysis: true,
    enableEcommerce: true,
    enableUpgradePrompts: true
  },
  domains: {
    panelSubdomain: 'panel',
    panelOrigin: '',
    portalOrigin: '',
    enforcePanelHost: false
  }
};
