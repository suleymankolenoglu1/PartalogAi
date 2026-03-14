# Partalog for WooCommerce

Bu plugin Partalog kataloglarini WooCommerce sayfalarina ekler ve secilen parcayi WooCommerce arama, urun veya sepet akisina baglar.

## Kurulum

1. Plugin klasorunu zip'le.
2. WordPress admin panelinde `Plugins > Add New > Upload Plugin` adimina git.
3. Zip dosyasini yukle ve aktifleştir.
4. `Settings > Partalog WooCommerce` ekranina gir.

## Temel ayarlar

Zorunlu alanlar:

- `Partalog API Base URL`
- `Embed Key`

Opsiyonel alanlar:

- `Calisma Modu`
- `Embed Yuksekligi`
- `Arama URL Sablonu`
- `Urun URL Sablonu`
- `Stok/Fiyat Gosterimi`

## Calisma modlari

- `Sadece Katalog`
- `Sitede Ara`
- `Urun Sayfasina Git`
- `WooCommerce Sepete Ekle`
- `WooCommerce Stok/Fiyat + Sepet`

## Kullanim

Partalog'u gostermek istedigin sayfaya su shortcode'u ekle:

```text
[partalog_embed]
```

Istersen sayfa bazli override kullanabilirsin:

```text
[partalog_embed embed_key="emb_xxx" mode="woocommerce_cart" height="820px"]
```

## Esleme mantigi

- `partCode` -> WooCommerce `SKU`
- `quantity` -> WooCommerce sepet adedi

Plugin icinde:

- `wp-json/partalog/v1/cart/add`
- `wp-json/partalog/v1/availability`

route'lari hazirdir.

## Hangi modu ne zaman secmeli

- `Sitede Ara`: Urunler Woo search ile bulunuyorsa
- `Urun Sayfasina Git`: Her parca icin net urun URL yapisi varsa
- `WooCommerce Sepete Ekle`: Urunleri dogrudan Woo sepetine eklemek istiyorsan
- `WooCommerce Stok/Fiyat + Sepet`: Partalog icinde Woo stok ve fiyatini da gostermek istiyorsan

## Test kontrol listesi

1. `partCode` ile eslesen bir Woo urununun `SKU` alanini kontrol et.
2. Shortcode eklenen sayfada katalog aciliyor mu kontrol et.
3. `Sitede Ara` modunda arama sayfasi dogru aciliyor mu kontrol et.
4. `Urun Sayfasina Git` modunda urun linki dogru aciliyor mu kontrol et.
5. `WooCommerce Sepete Ekle` modunda mini cart veya sepet sayaci artiyor mu kontrol et.
6. `WooCommerce Stok/Fiyat + Sepet` modunda stok ve fiyat gorunuyor mu kontrol et.

## Notlar

- `partCode`, WooCommerce urun `SKU` alani ile eslestirilir.
- `WooCommerce Sepete Ekle` modu, plugin icindeki REST bridge route'unu kullanir.
- Ilk surum tek embed instance odaklidir.
