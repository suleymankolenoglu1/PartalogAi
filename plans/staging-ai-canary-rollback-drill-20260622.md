# Staging AI Canary / Rollback Drill — 2026-06-22

## Sonuç

- Status: `passed`
- Kapsam: `partalog-ai-chat-staging` Cloud Run servisi
- Environment: Google Cloud staging
- Final active revision: `partalog-ai-chat-staging-00012-crn`
- Rollback target revision: `partalog-ai-chat-staging-00010-6rh`
- Final traffic: `00012-crn = 100%`
- Final scaling: service min/max `1/1`, revision max `1`
- Final health: `/health/ready` passed
- Error log scan: tatbikat aralığında `ERROR`, `Exception`, `Traceback` kaydı bulunmadı.

## Amaç

Production canary öncesinde Cloud Run üzerinde üç şeyi panik anı gelmeden
kanıtlamak:

1. Yeni revision'ın trafiğe açılmadan/tag üzerinden kontrol edilebilmesi.
2. Sorun anında eski revision'a hızlı rollback yapılabilmesi.
3. Rollback sonrası yeni revision'a tekrar roll-forward yapılabilmesi.

Bu tatbikat production'a dokunmadan staging private AI servisi üzerinde yapıldı.

## Revision'lar

| rol | revision | not |
|---|---|---|
| candidate | `partalog-ai-chat-staging-00012-crn` | `ai-staging-a12bd1c-vector-contract` image'ı, final aktif revision |
| rollback | `partalog-ai-chat-staging-00010-6rh` | SSE cold-start remediation sonrası bilinen iyi revision |

`00012-crn`, canary split denemesi için geçici max-instance ayarı uygulanırken
oluştu. Image contract'ı `00011-9q9` ile aynı vector-contract build hattından
geliyor; finalde max-instance tekrar `1` seviyesine döndürüldü.

## Bulgular

Yüzdelik canary split ilk denemede Cloud Run tarafından reddedildi:

```text
metadata.annotations[run.googleapis.com/maxScale]: service level max instances
must be greater than or equal to the number of targets receiving traffic.
```

Bu beklenen ve faydalı bir guardrail. Staging AI servisinde maliyet/sadelik için
service-level `maxScale=1` tutulduğu için iki revision'a aynı anda yüzdeli
trafik verilemiyor. Bu yüzden tatbikat güvenli alternatif akışla tamamlandı:

- tagged canary health check
- tek hedefli `%100` rollback
- tek hedefli `%100` roll-forward

Production'da gerçek yüzdelik canary istenirse, canary süresince service-level
maxScale değerinin en az trafik alan revision hedef sayısı kadar olması gerekir.

## Adımlar ve Kanıt

### 1. Baseline health

Aktif servis `/health/ready` kontrolü geçti:

- DB ready: `true`
- DB mode: `configured`
- Redis capacity ready: `true`
- capacity mode: `redis-distributed`

### 2. Tagged canary

Cloud Run tag'leri oluşturuldu:

- `candidate` → `partalog-ai-chat-staging-00012-crn`
- `rollback` → `partalog-ai-chat-staging-00010-6rh`

Candidate tag URL üzerinden private `/health/ready` kontrolü geçti.

### 3. Rollback

Trafik geçici olarak eski revision'a alındı:

```text
100% partalog-ai-chat-staging-00010-6rh
0%   partalog-ai-chat-staging-00012-crn
```

Rollback sonrası `/health/ready` geçti:

- DB ready: `true`
- Redis capacity ready: `true`
- active chats: `0`
- saturated: `false`

### 4. Roll-forward

Trafik yeni revision'a geri alındı:

```text
100% partalog-ai-chat-staging-00012-crn
0%   partalog-ai-chat-staging-00010-6rh
```

Final `/health/ready` geçti.

### 5. Final durum

Cloud Run final read-back:

- service `run.googleapis.com/minScale`: `1`
- service `run.googleapis.com/maxScale`: `1`
- revision `autoscaling.knative.dev/maxScale`: `1`
- traffic:
  - `partalog-ai-chat-staging-00012-crn`: `100%`
  - `candidate` tag: `00012-crn`
  - `rollback` tag: `00010-6rh`

## Production İçin Çıkarım

- Rollback komutu ve roll-forward komutu staging'de doğrulandı.
- Yüzdelik canary için production'da kısa süreli `maxScale >= 2` planlanmalı.
- Eğer maliyet hassasiyeti yüzünden `maxScale=1` kalacaksa, production'da ilk
  geçiş stratejisi tagged canary + hızlı rollback şeklinde olmalı.
- Notification channel hâlâ ayrı bir production readiness maddesi olarak açık:
  incident Cloud Monitoring'de açılıyor, ama e-posta/Slack/PagerDuty gibi bir
  kanala henüz bağlanmıyor.
