<?php

if (!defined('ABSPATH')) {
    exit;
}

class Partalog_WooCommerce_Product_Mapper
{
    public function get_product_id_by_part_code(string $part_code): int
    {
        $normalized = trim($part_code);
        if ($normalized === '') {
            return 0;
        }

        if (!function_exists('wc_get_product_id_by_sku')) {
            return 0;
        }

        return (int) wc_get_product_id_by_sku($normalized);
    }

    public function get_product_by_part_code(string $part_code): ?WC_Product
    {
        $product_id = $this->get_product_id_by_part_code($part_code);
        if ($product_id <= 0) {
            return null;
        }

        $product = wc_get_product($product_id);
        return $product instanceof WC_Product ? $product : null;
    }
}
