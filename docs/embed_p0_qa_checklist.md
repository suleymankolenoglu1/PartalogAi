# Embed P0 QA Checklist

## 1. Duz HTML testi
- `embed-test.html` ac
- iframe ilk yuklemede kirpilmadan gorunmeli
- chat acildiginda iframe yuksekligi artmali
- chat kapatildiginda bos alan birakmamali
- katalog viewer'a gidince teknik resim ve parca listesi tam gorunmeli
- checkout sayfasina gecince iframe yuksekligi tekrar guncellenmeli

## 2. Domain/origin testi
- allowlist disi domainde hata karti gorunmeli
- allowlist icindeki domainde vitrin normal acilmali
- store slug ile calisiyorsa token yazmadan acilmali

## 3. Plan testi
- Plan 1: embed calismali, Powered by Partalog gorunmeli
- Plan 2: embed calismali, Powered by Partalog gorunmeli
- Plan 3: embed calismali, white-label olmali

## 4. Mobil testi
- dar ekranda iframe yatay tasma yapmamali
- public vitrin mobilde okunabilir kalmali
- checkout mobilde alttan kirpilmamali

## 5. Event testi
- katalog parcasina tiklayinca `part:viewed`
- sepete ekleyince `cart:add`
- checkout acilinca `checkout:start`

## Beklenen sonuc
- iframe yuksekligi manuel sabit degere bagli hissettirmemeli
- host sayfa scroll'u ile embed icerigi birbiriyle kavga etmemeli
- kirpilma, cift scroll veya altta buyuk bosluk olmamali
