# Backend Migration Discipline

Bu projede veritabanı şeması sadece EF migration ile yönetilir.

## Kurallar

- `Program.cs` içinde runtime `ALTER TABLE` / `CREATE TABLE` patch kodu bulunmaz.
- Uygulama startup'ta:
  - pending migration varsa uygular (`db.Database.Migrate()`),
  - migration sonrası hâlâ pending varsa fail eder,
  - `HasPendingModelChanges()` true ise fail eder.

## Lokal Kontrol

```bash
cd /Users/suleymankolenoglu/Desktop/Projeler/Katalogcu

dotnet build backend/Katalogcu.API/Katalogcu.API.csproj --no-restore

dotnet ef migrations has-pending-model-changes \
  --project backend/Katalogcu.Infrastructure/Katalogcu.Infrastructure.csproj \
  --startup-project backend/Katalogcu.API/Katalogcu.API.csproj \
  --context AppDbContext
```

## Migration Bundle Üretme

```bash
cd /Users/suleymankolenoglu/Desktop/Projeler/Katalogcu
mkdir -p backend/artifacts

dotnet ef migrations bundle \
  --project backend/Katalogcu.Infrastructure/Katalogcu.Infrastructure.csproj \
  --startup-project backend/Katalogcu.API/Katalogcu.API.csproj \
  --context AppDbContext \
  --self-contained false \
  --output backend/artifacts/efbundle
```

## Bundle Uygulama

```bash
backend/artifacts/efbundle \
  --connection "Host=127.0.0.1;Port=5432;Database=KatalogcuDb;Username=postgres;Password=CHANGE_ME"
```

CI tarafında aynı disiplin `/Users/suleymankolenoglu/Desktop/Projeler/Katalogcu/.github/workflows/backend-migration-discipline.yml` ile enforce edilir.
