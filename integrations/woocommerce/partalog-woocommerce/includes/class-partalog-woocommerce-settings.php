<?php

if (!defined('ABSPATH')) {
    exit;
}

class Partalog_WooCommerce_Settings
{
    public const OPTION_KEY = 'partalog_woocommerce_settings';

    public function register(): void
    {
        add_action('admin_menu', [$this, 'register_menu']);
        add_action('admin_init', [$this, 'register_settings']);
    }

    public function get_settings(): array
    {
        $defaults = [
            'api_base_url' => '',
            'embed_key' => '',
            'mode' => 'catalog_only',
            'embed_height' => '780px',
            'search_url_template' => '/?s={partCode}&post_type=product',
            'product_url_template' => '',
            'enable_availability' => false,
        ];

        $settings = get_option(self::OPTION_KEY, []);
        return wp_parse_args(is_array($settings) ? $settings : [], $defaults);
    }

    public function register_menu(): void
    {
        add_options_page(
            'Partalog WooCommerce',
            'Partalog WooCommerce',
            'manage_options',
            'partalog-woocommerce',
            [$this, 'render_settings_page']
        );
    }

    public function register_settings(): void
    {
        register_setting(
            'partalog_woocommerce_group',
            self::OPTION_KEY,
            [$this, 'sanitize_settings']
        );

        add_settings_section(
            'partalog_woocommerce_main',
            'Partalog Ayarlari',
            function (): void {
                echo '<p>Embed key, API adresi ve WooCommerce davranışını bu ekrandan yönetebilirsin.</p>';
            },
            'partalog-woocommerce'
        );

        $fields = [
            'api_base_url' => 'Partalog API Base URL',
            'embed_key' => 'Embed Key',
            'mode' => 'Çalışma Modu',
            'embed_height' => 'Embed Yüksekliği',
            'search_url_template' => 'Arama URL Şablonu',
            'product_url_template' => 'Ürün URL Şablonu',
            'enable_availability' => 'Stok/Fiyat Gösterimi',
        ];

        foreach ($fields as $key => $label) {
            add_settings_field(
                $key,
                $label,
                [$this, 'render_field'],
                'partalog-woocommerce',
                'partalog_woocommerce_main',
                ['key' => $key]
            );
        }
    }

    public function sanitize_settings(array $input): array
    {
        return [
            'api_base_url' => esc_url_raw(trim((string) ($input['api_base_url'] ?? ''))),
            'embed_key' => sanitize_text_field((string) ($input['embed_key'] ?? '')),
            'mode' => $this->sanitize_mode((string) ($input['mode'] ?? 'catalog_only')),
            'embed_height' => sanitize_text_field((string) ($input['embed_height'] ?? '780px')),
            'search_url_template' => sanitize_text_field((string) ($input['search_url_template'] ?? '')),
            'product_url_template' => sanitize_text_field((string) ($input['product_url_template'] ?? '')),
            'enable_availability' => !empty($input['enable_availability']),
        ];
    }

    private function sanitize_mode(string $mode): string
    {
        $allowed = [
            'catalog_only',
            'search_redirect',
            'product_redirect',
            'woocommerce_cart',
            'woocommerce_availability_cart',
        ];

        return in_array($mode, $allowed, true) ? $mode : 'catalog_only';
    }

    public function render_field(array $args): void
    {
        $key = (string) ($args['key'] ?? '');
        $settings = $this->get_settings();
        $value = $settings[$key] ?? '';
        $option_name = self::OPTION_KEY . '[' . $key . ']';

        if ($key === 'mode') {
            echo '<select name="' . esc_attr($option_name) . '" class="regular-text">';
            $options = [
                'catalog_only' => 'Sadece Katalog',
                'search_redirect' => 'Sitede Ara',
                'product_redirect' => 'Ürün Sayfasına Git',
                'woocommerce_cart' => 'WooCommerce Sepete Ekle',
                'woocommerce_availability_cart' => 'WooCommerce Stok/Fiyat + Sepet',
            ];

            foreach ($options as $option_value => $label) {
                printf(
                    '<option value="%s" %s>%s</option>',
                    esc_attr($option_value),
                    selected($value, $option_value, false),
                    esc_html($label)
                );
            }

            echo '</select>';
            return;
        }

        if ($key === 'enable_availability') {
            printf(
                '<label><input type="checkbox" name="%s" value="1" %s /> WooCommerce ürün stok ve fiyat bilgisini Partalog içine aktar</label>',
                esc_attr($option_name),
                checked((bool) $value, true, false)
            );
            return;
        }

        printf(
            '<input type="text" class="regular-text" name="%s" value="%s" />',
            esc_attr($option_name),
            esc_attr((string) $value)
        );
    }

    public function render_settings_page(): void
    {
        echo '<div class="wrap">';
        echo '<h1>Partalog for WooCommerce</h1>';
        echo '<p>Kullanım: bir sayfaya <code>[partalog_embed]</code> ekleyin.</p>';
        echo '<form method="post" action="options.php">';
        settings_fields('partalog_woocommerce_group');
        do_settings_sections('partalog-woocommerce');
        submit_button('Kaydet');
        echo '</form>';
        echo '</div>';
    }
}
