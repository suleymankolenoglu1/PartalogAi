<?php

if (!defined('ABSPATH')) {
    exit;
}

class Partalog_WooCommerce_Plugin
{
    private static ?Partalog_WooCommerce_Plugin $instance = null;

    private Partalog_WooCommerce_Settings $settings;
    private Partalog_WooCommerce_Shortcode $shortcode;
    private Partalog_WooCommerce_REST_Controller $rest_controller;
    private Partalog_WooCommerce_Product_Mapper $product_mapper;
    private Partalog_WooCommerce_Cart_Service $cart_service;

    public static function instance(): Partalog_WooCommerce_Plugin
    {
        if (self::$instance === null) {
            self::$instance = new self();
        }

        return self::$instance;
    }

    private function __construct()
    {
        $this->settings = new Partalog_WooCommerce_Settings();
        $this->product_mapper = new Partalog_WooCommerce_Product_Mapper();
        $this->cart_service = new Partalog_WooCommerce_Cart_Service($this->product_mapper);
        $this->shortcode = new Partalog_WooCommerce_Shortcode($this);
        $this->rest_controller = new Partalog_WooCommerce_REST_Controller($this->product_mapper, $this->cart_service);

        add_action('plugins_loaded', [$this, 'boot']);
    }

    public function boot(): void
    {
        if (!class_exists('WooCommerce')) {
            add_action('admin_notices', [$this, 'render_woocommerce_notice']);
            return;
        }

        $this->settings->register();
        $this->shortcode->register();
        $this->rest_controller->register();
    }

    public function render_woocommerce_notice(): void
    {
        echo '<div class="notice notice-error"><p>Partalog for WooCommerce eklentisi için WooCommerce aktif olmalıdır.</p></div>';
    }

    public function get_settings(): array
    {
        return $this->settings->get_settings();
    }

    public function get_setting(string $key, $default = '')
    {
        $settings = $this->get_settings();
        return array_key_exists($key, $settings) ? $settings[$key] : $default;
    }

    public function get_api_base_url(): string
    {
        return rtrim((string) $this->get_setting('api_base_url', ''), '/');
    }

    public function get_embed_key(): string
    {
        return trim((string) $this->get_setting('embed_key', ''));
    }

    public function get_mode(): string
    {
        return (string) $this->get_setting('mode', 'catalog_only');
    }

    public function get_embed_height(): string
    {
        return (string) $this->get_setting('embed_height', '780px');
    }

    public function get_search_url_template(): string
    {
        return (string) $this->get_setting('search_url_template', '/?s={partCode}&post_type=product');
    }

    public function get_product_url_template(): string
    {
        return (string) $this->get_setting('product_url_template', '');
    }

    public function should_enable_availability(): bool
    {
        return (bool) $this->get_setting('enable_availability', false);
    }

    public function get_frontend_config(array $overrides = []): array
    {
        $config = [
            'apiBaseUrl' => $this->get_api_base_url(),
            'embedKey' => $this->get_embed_key(),
            'mode' => $this->get_mode(),
            'height' => $this->get_embed_height(),
            'searchUrlTemplate' => $this->get_search_url_template(),
            'productUrlTemplate' => $this->get_product_url_template(),
            'enableAvailability' => $this->should_enable_availability(),
            'addToCartUrl' => rest_url('partalog/v1/cart/add'),
            'availabilityUrl' => rest_url('partalog/v1/availability'),
        ];

        return array_merge($config, $overrides);
    }
}
