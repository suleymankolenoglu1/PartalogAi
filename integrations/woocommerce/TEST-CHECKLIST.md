# WooCommerce Test Checklist

## 1. Plugin kurulumu

- Zip dosyasi WordPress'e yuklenebiliyor mu
- Plugin aktiflesiyor mu
- `Settings > Partalog WooCommerce` ekrani gorunuyor mu

## 2. Ayar testi

- `API Base URL` kaydediliyor mu
- `Embed Key` kaydediliyor mu
- Mod secimi kaydediliyor mu

## 3. Katalog gosterimi

- `[partalog_embed]` olan sayfada katalog aciliyor mu
- Dogru katalog veya sayfa geliyor mu
- Domain izinleri nedeniyle bloklanma var mi

## 4. Search redirect testi

- Mod `Sitede Ara` oldugunda parca secimi search sonucuna gidiyor mu
- `partCode` query olarak tasiniyor mu

## 5. Product redirect testi

- Mod `Urun Sayfasina Git` oldugunda parca dogru urune gidiyor mu
- URL sablonu gercek SKU/part code ile calisiyor mu

## 6. WooCommerce cart testi

- Woo urun SKU alani `partCode` ile uyusuyor mu
- `Sepete` butonu urunu dogrudan Woo sepetine ekliyor mu
- Sepet sayaci artiyor mu

## 7. Availability testi

- Mod `WooCommerce Stok/Fiyat + Sepet` oldugunda stok bilgisi geliyor mu
- Fiyat bilgisi geliyor mu
- Stokta olmayan urun icin `canAddToCart=false` donuyor mu
