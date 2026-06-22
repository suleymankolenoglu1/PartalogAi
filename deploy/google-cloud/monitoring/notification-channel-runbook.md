# Monitoring Notification Channel Runbook

Bu runbook Partalog Cloud Monitoring alert policy'lerini e-posta tabanlı bir
notification channel'a bağlamak için kullanılır.

## Mevcut Durum

2026-06-22 kontrolünde `partalog` projesinde Monitoring notification channel
bulunmadı:

```text
notification_channel_count=0
```

Alert policy'ler aktif olduğu için incident Cloud Monitoring içinde açılır; ancak
kanal bağlı olmadığı sürece e-posta/Slack/PagerDuty gibi dış bildirim gitmez.

## Gereken Operatör Kararı

Kanal oluşturmadan önce production on-call alıcısı seçilmeli:

- ekip e-posta grubu, örn. `ops@example.com`
- bireysel e-posta, geçici kullanım için
- Slack/PagerDuty gibi farklı kanal, ileride ayrıca eklenebilir

Otomasyon kişisel adres tahmin etmemelidir. E-posta notification channel'ları
Google Cloud tarafında alıcı doğrulaması isteyebilir; doğrulanmadan production
go-live gate'i tamamlanmış sayılmamalıdır.

## E-posta Kanalı Oluşturma ve Policy'lere Bağlama

Staging alert policy'leri için:

```bash
python3 deploy/google-cloud/monitoring/create_email_notification_channel.py \
  --project partalog \
  --email-address "ops@example.com" \
  --display-name "Partalog production on-call email"
```

Script şunları yapar:

1. Aynı e-posta için mevcut channel varsa reuse eder.
2. Yoksa email notification channel oluşturur.
3. Şu policy'lere `notificationChannels` olarak bağlar:
   - `Partalog staging public availability`
   - `Partalog staging Cloud Run reliability`
4. Channel verification status değerini yazar.

Adresler loglarda redakte edilir.

## Dry Run

Gerçek değişiklik yapmadan planı görmek için:

```bash
python3 deploy/google-cloud/monitoring/create_email_notification_channel.py \
  --project partalog \
  --email-address "ops@example.com" \
  --dry-run
```

## Doğrulama

Bağlama sonrası policy'leri okuyup `notificationChannels` alanının dolu olduğunu
kontrol et:

```bash
.tools/google-cloud-sdk/bin/gcloud monitoring policies list \
  --project=partalog \
  --format='table(displayName,enabled,notificationChannels)'
```

E-posta channel doğrulanmamış görünürse Google Cloud Console'dan veya alıcı
mailbox'ından verification işlemini tamamla.

## Production Canary Öncesi Gate

Production canary'ye geçmeden önce minimum kabul kriteri:

- en az bir active notification channel var,
- production/on-call sahibi tarafından doğrulanmış,
- staging reliability ve availability policy'lerine bağlı,
- tercihen production policy'leri oluşturulduğunda aynı kanala bağlanacak şekilde
  runbook güncel.
