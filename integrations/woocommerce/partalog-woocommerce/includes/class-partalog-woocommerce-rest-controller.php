<?php

if (!defined('ABSPATH')) {
    exit;
}

class Partalog_WooCommerce_REST_Controller
{
    private Partalog_WooCommerce_Product_Mapper $product_mapper;
    private Partalog_WooCommerce_Cart_Service $cart_service;

    public function __construct(
        Partalog_WooCommerce_Product_Mapper $product_mapper,
        Partalog_WooCommerce_Cart_Service $cart_service
    ) {
        $this->product_mapper = $product_mapper;
        $this->cart_service = $cart_service;
    }

    public function register(): void
    {
        add_action('rest_api_init', [$this, 'register_routes']);
    }

    public function register_routes(): void
    {
        register_rest_route('partalog/v1', '/cart/add', [
            'methods' => 'POST',
            'callback' => [$this, 'handle_add_to_cart'],
            'permission_callback' => '__return_true',
        ]);

        register_rest_route('partalog/v1', '/availability', [
            'methods' => 'POST',
            'callback' => [$this, 'handle_availability'],
            'permission_callback' => '__return_true',
        ]);
    }

    public function handle_add_to_cart(WP_REST_Request $request): WP_REST_Response
    {
        $payload = $request->get_json_params();
        $part_code = sanitize_text_field((string) ($payload['partCode'] ?? ''));
        $quantity = max(1, (int) ($payload['quantity'] ?? 1));

        $result = $this->cart_service->add_item_by_part_code($part_code, $quantity);
        $status = (int) ($result['status'] ?? 200);
        unset($result['status']);

        return new WP_REST_Response($result, $status);
    }

    public function handle_availability(WP_REST_Request $request): WP_REST_Response
    {
        $payload = $request->get_json_params();
        $items = is_array($payload['items'] ?? null) ? $payload['items'] : [];
        $currency = function_exists('get_woocommerce_currency') ? get_woocommerce_currency() : 'TRY';

        $response_items = [];
        foreach ($items as $item) {
            $catalog_item_id = sanitize_text_field((string) ($item['catalogItemId'] ?? ''));
            $part_code = sanitize_text_field((string) ($item['partCode'] ?? ''));
            $product = $this->product_mapper->get_product_by_part_code($part_code);

            if (!$product) {
                $response_items[] = [
                    'catalogItemId' => $catalog_item_id ?: null,
                    'partCode' => $part_code ?: null,
                    'stockStatus' => 'out_of_stock',
                    'availabilityLabel' => 'Eslesme bulunamadi',
                    'unitPrice' => null,
                    'currency' => $currency,
                    'canAddToCart' => false,
                ];
                continue;
            }

            $stock_status = 'out_of_stock';
            $availability_label = 'Stokta yok';

            if ($product->is_in_stock()) {
                $stock_status = 'in_stock';
                $availability_label = 'Stokta var';
            } elseif ($product->backorders_allowed()) {
                $stock_status = 'available_to_order';
                $availability_label = 'Siparişe uygun';
            }

            $response_items[] = [
                'catalogItemId' => $catalog_item_id ?: null,
                'partCode' => $part_code ?: null,
                'stockStatus' => $stock_status,
                'availabilityLabel' => $availability_label,
                'unitPrice' => $product->get_price() !== '' ? (float) $product->get_price() : null,
                'currency' => $currency,
                'canAddToCart' => $product->is_purchasable() && ($product->is_in_stock() || $product->backorders_allowed()),
            ];
        }

        return new WP_REST_Response(['items' => $response_items], 200);
    }
}
