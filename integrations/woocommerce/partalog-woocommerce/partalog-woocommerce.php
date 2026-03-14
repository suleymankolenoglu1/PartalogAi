<?php
/**
 * Plugin Name: Partalog for WooCommerce
 * Description: Embeds Partalog catalogs into WooCommerce pages and connects catalog actions to WooCommerce search, product, cart, and stock flows.
 * Version: 0.1.0
 * Author: Partalog
 * Requires Plugins: woocommerce
 */

if (!defined('ABSPATH')) {
    exit;
}

define('PARTALOG_WOO_VERSION', '0.1.0');
define('PARTALOG_WOO_PLUGIN_FILE', __FILE__);
define('PARTALOG_WOO_PLUGIN_DIR', plugin_dir_path(__FILE__));
define('PARTALOG_WOO_PLUGIN_URL', plugin_dir_url(__FILE__));

require_once PARTALOG_WOO_PLUGIN_DIR . 'includes/class-partalog-woocommerce-plugin.php';
require_once PARTALOG_WOO_PLUGIN_DIR . 'includes/class-partalog-woocommerce-settings.php';
require_once PARTALOG_WOO_PLUGIN_DIR . 'includes/class-partalog-woocommerce-shortcode.php';
require_once PARTALOG_WOO_PLUGIN_DIR . 'includes/class-partalog-woocommerce-rest-controller.php';
require_once PARTALOG_WOO_PLUGIN_DIR . 'includes/class-partalog-woocommerce-product-mapper.php';
require_once PARTALOG_WOO_PLUGIN_DIR . 'includes/class-partalog-woocommerce-cart-service.php';

Partalog_WooCommerce_Plugin::instance();
