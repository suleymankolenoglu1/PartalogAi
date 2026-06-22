# Production Notification Channel Prep — 2026-06-22

## Sonuç

- Status: `attached_pending_mailbox_verification`
- Kapsam: Google Cloud Monitoring notification channel readiness
- Cloud Monitoring alert policies: `2 active`
- Notification channel read-back: `1 email channel`
- Channel: `projects/partalog/notificationChannels/542323106088939530`
- Recipient: `info@partalog.tech`
- Policy attachment: `completed`
- Mailbox/Console verification: `manual check required`

## Mevcut Durum

Staging için iki alert policy aktif:

- `Partalog staging public availability`
- `Partalog staging Cloud Run reliability`

Bu policy'ler incident açabilir ve artık `info@partalog.tech` için oluşturulan
email notification channel'a bağlıdır.

Monitoring API read-back:

```text
notification_channel_count=1
channel=projects/partalog/notificationChannels/542323106088939530
type=email
enabled=True
recipient=info@partalog.tech
```

## Yapılan Hazırlık ve Uygulama

- E-posta notification channel oluşturup mevcut alert policy'lere bağlayan helper
  script eklendi:
  - `deploy/google-cloud/monitoring/create_email_notification_channel.py`
- Operasyon runbook'u eklendi:
  - `deploy/google-cloud/monitoring/notification-channel-runbook.md`
- Staging observability dokümanı channel gap + yeni runbook/script ile
  güncellendi.
- `info@partalog.tech` için email notification channel oluşturuldu.
- Channel mevcut staging policy'lere bağlandı:
  - `Partalog staging public availability`
  - `Partalog staging Cloud Run reliability`
- Helper script yeniden çalıştırıldığında channel'ı reuse edip policy'lerin zaten
  bağlı olduğunu doğruladı; duplicate channel oluşturmadı.

## Kullanım

Alıcı değişirse veya yeni ortamda tekrar uygulanırsa:

```bash
python3 deploy/google-cloud/monitoring/create_email_notification_channel.py \
  --project partalog \
  --email-address "ops@example.com" \
  --display-name "Partalog production on-call email"
```

Script:

1. aynı e-posta için mevcut channel varsa reuse eder,
2. yoksa yeni email notification channel oluşturur,
3. staging availability/reliability policy'lerine bağlar,
4. verification status bilgisini raporlar,
5. e-posta adresini loglarda redakte eder.

## Doğrulama

Policy read-back:

```text
Partalog staging Cloud Run reliability -> notificationChannels=1
Partalog staging public availability -> notificationChannels=1
```

Helper script read-back:

```text
Reusing existing email notification channel
Policy already attached: Partalog staging public availability
Policy already attached: Partalog staging Cloud Run reliability
```

## Kalan Manuel Kontrol

Google Cloud API `verificationStatus` alanını bu read-back'te döndürmedi. Bu
yüzden `info@partalog.tech` mailbox'ı veya Google Cloud Console üzerinden kanalın
doğrulama durumunun kontrol edilmesi gerekir.

E-posta doğrulaması gerekiyorsa tamamlanmadan production go-live gate'i tamamlanmış
sayılmamalıdır.

## Sıradaki Karar

- `info@partalog.tech` mailbox'ında Google Cloud doğrulama maili var mı kontrol
  edilmeli.
- Production policy'leri oluşturulurken aynı channel'a bağlanmalı veya ayrı bir
  production-only on-call channel seçilmeli.
