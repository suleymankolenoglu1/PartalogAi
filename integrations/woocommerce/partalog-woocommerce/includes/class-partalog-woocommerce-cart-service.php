<?php

if (!defined('ABSPATH')) {
    exit;
}

class Partalog_WooCommerce_Cart_Service
{
    private Partalog_WooCommerce_Product_Mapper $product_mapper;

    public function __construct(Partalog_WooCommerce_Product_Mapper $product_mapper)
    {
        $this->product_mapper = $product_mapper;
    }

    public function add_item_by_part_code(string $part_code, int $quantity): array
    {
        $this->ensure_cart_ready();

        $product = $this->product_mapper->get_product_by_part_code($part_code);
        if (!$product) {
            return [
                'success' => false,
                'status' => 404,
                'message' => 'Parça kodu WooCommerce ürün SKU alanında bulunamadı.',
            ];
        }

        if (!$product->is_purchasable()) {
            return [
                'success' => false,
                'status' => 409,
                'message' => 'Bu ürün satın alınabilir durumda değil.',
            ];
        }

        if (!$product->is_in_stock() && !$product->backorders_allowed()) {
            return [
                'success' => false,
                'status' => 409,
                'message' => 'Ürün stokta yok.',
            ];
        }

        $quantity = max(1, $quantity);
        $result = WC()->cart->add_to_cart($product->get_id(), $quantity);
        if (!$result) {
            return [
                'success' => false,
                'status' => 500,
                'message' => 'WooCommerce sepetine ekleme başarısız oldu.',
            ];
        }

        return [
            'success' => true,
            'status' => 200,
            'message' => sprintf('%s WooCommerce sepetine eklendi.', $part_code),
            'cartCount' => (int) WC()->cart->get_cart_contents_count(),
        ];
    }

    public function ensure_cart_ready(): void
    {
        if (function_exists('wc_load_cart') && (WC()->cart === null || WC()->session === null)) {
            wc_load_cart();
        }
    }
}
