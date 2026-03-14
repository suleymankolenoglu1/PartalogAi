<?php

if (!defined('ABSPATH')) {
    exit;
}

class Partalog_WooCommerce_Shortcode
{
    private Partalog_WooCommerce_Plugin $plugin;

    public function __construct(Partalog_WooCommerce_Plugin $plugin)
    {
        $this->plugin = $plugin;
    }

    public function register(): void
    {
        add_shortcode('partalog_embed', [$this, 'render']);
    }

    public function render(array $atts = []): string
    {
        $defaults = [
            'embed_key' => $this->plugin->get_embed_key(),
            'height' => $this->plugin->get_embed_height(),
            'mode' => $this->plugin->get_mode(),
        ];

        $atts = shortcode_atts($defaults, $atts, 'partalog_embed');
        $config = $this->plugin->get_frontend_config([
            'embedKey' => trim((string) $atts['embed_key']),
            'height' => trim((string) $atts['height']),
            'mode' => trim((string) $atts['mode']),
        ]);

        if (empty($config['apiBaseUrl']) || empty($config['embedKey'])) {
            return current_user_can('manage_options')
                ? '<div class="partalog-woo-notice">Partalog ayarlarında API Base URL ve Embed Key zorunludur.</div>'
                : '';
        }

        $container_id = 'partalog-embed-' . wp_generate_uuid4();
        $bootstrap = [
            'apiBaseUrl' => $config['apiBaseUrl'],
            'mode' => $config['mode'],
            'searchUrlTemplate' => $config['searchUrlTemplate'],
            'productUrlTemplate' => $config['productUrlTemplate'],
            'enableAvailability' => (bool) $config['enableAvailability'],
            'addToCartUrl' => $config['addToCartUrl'],
            'availabilityUrl' => $config['availabilityUrl'],
        ];

        ob_start();
        ?>
        <div id="<?php echo esc_attr($container_id); ?>"></div>
        <script>
          window.PartalogWooEmbedConfig = <?php echo wp_json_encode($bootstrap); ?>;
        </script>
        <script src="<?php echo esc_url(PARTALOG_WOO_PLUGIN_URL . 'assets/js/partalog-woo.js'); ?>"></script>
        <?php if ($config['mode'] !== 'catalog_only') : ?>
          <script src="<?php echo esc_url($config['apiBaseUrl'] . '/host-adapter.js'); ?>"></script>
        <?php endif; ?>
        <script
          src="<?php echo esc_url($config['apiBaseUrl'] . '/embed.js'); ?>"
          data-embed-key="<?php echo esc_attr((string) $config['embedKey']); ?>"
          data-api-base-url="<?php echo esc_attr((string) $config['apiBaseUrl']); ?>"
          data-target="#<?php echo esc_attr($container_id); ?>"
          data-height="<?php echo esc_attr((string) $config['height']); ?>"></script>
        <?php

        return (string) ob_get_clean();
    }
}
