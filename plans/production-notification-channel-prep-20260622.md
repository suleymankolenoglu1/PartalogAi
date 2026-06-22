# Production Notification Channel Prep — 2026-06-22

## Sonuç

- Status: `blocked_on_recipient`
- Kapsam: Google Cloud Monitoring notification channel readiness
- Cloud Monitoring alert policies: `2 active`
- Notification channel read-back: `0 channels`
- Repo hazırlığı: `completed`
- Gerçek kanal bağlama: production/on-call alıcısı seçilene kadar bekliyor

## Mevcut Durum

Staging için iki alert policy aktif:

- `Partalog staging public availability`
- `Partalog staging Cloud Run reliability`

Bu policy'ler incident açabilir, ancak projede notification channel bulunmadığı
için e-posta/Slack/PagerDuty gibi dış bir kanala bildirim gitmez.

Monitoring API read-back:

```text
notification_channel_count=0
```

## Yapılan Hazırlık

- E-posta notification channel oluşturup mevcut alert policy'lere bağlayan helper
  script eklendi:
  - `deploy/google-cloud/monitoring/create_email_notification_channel.py`
- Operasyon runbook'u eklendi:
  - `deploy/google-cloud/monitoring/notification-channel-runbook.md`
- Staging observability dokümanı channel gap + yeni runbook/script ile
  güncellendi.

## Kullanım

Production/on-call alıcısı seçildikten sonra:

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

## Blocker

Gerçek notification channel oluşturmak için operatörün bir alıcı seçmesi gerekir.
Otomasyon kişisel adres tahmin etmemeli veya kullanıcının açık onayı olmadan dış
bildirim alıcısı oluşturmamalıdır.

E-posta channel oluşturulduktan sonra Google Cloud doğrulaması gerekebilir. Bu
doğrulama tamamlanmadan production go-live gate'i tamamlanmış sayılmamalıdır.

## Sıradaki Karar

Kullanıcı/operatör şunlardan birini seçmeli:

- production ekip e-posta grubu,
- geçici bireysel e-posta,
- Slack/PagerDuty gibi farklı kanal sağlayıcısı.

E-posta seçilirse script ile aynı adımda channel oluşturma ve policy bağlama
tamamlanabilir.
