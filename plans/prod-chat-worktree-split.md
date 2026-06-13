# Prod Chat Worktree Split

Date: 2026-06-13

## Goal

Create a small, reviewable prod-chat release package from the current dirty worktree without reverting unrelated user work.

## Current State

The worktree is not a single release package. It contains at least these streams at the same time:

- Prod chat reliability and deployment hardening.
- DataProtection Redis key-ring and XML encryption.
- AI chat service decomposition and lightweight chat image.
- Chat evaluation and stream-contract work.
- External matching, crawling, compatibility, customer machine, and policy threshold admin work.
- Frontend redesign/public-view work.
- Generated/static visual part file deletions and local screenshots.

Approximate status shape from `git status --short`:

- `backend`: many modified, deleted, and untracked files.
- `frontend`: many modified files plus a few untracked stream-state files.
- `partalog-ai`: modified chat/eval files plus many new service/test files.
- Static/generated assets: many deleted `backend/Katalogcu.API/wwwroot/static/visual-parts/...` files.

## Prod Chat Package Candidates

These files are strong candidates for the prod-chat package.

### Backend API infrastructure

- `backend/Katalogcu.API/Program.cs`
- `backend/Katalogcu.API/Katalogcu.API.csproj`
- `backend/Katalogcu.API/appsettings.example.json`
- `backend/Katalogcu.API/Services/AesGcmDataProtectionXmlEncryptor.cs`
- `backend/Katalogcu.API/Services/DataProtectionKeyRingOptions.cs`
- `backend/Katalogcu.API/Services/RedisDataProtectionXmlRepository.cs`
- `backend/Katalogcu.API/Services/AiCapacityGuard.cs`
- `backend/Katalogcu.API/Services/DistributedPublicChatRateLimiter.cs`
- `backend/Katalogcu.API/Services/AiServiceOptions.cs`
- `backend/Katalogcu.API/Services/ProductionReadinessService.cs`
- `backend/Katalogcu.API/Services/JwtSecretResolver.cs`
- `backend/Katalogcu.API/Controllers/SystemController.cs`

### Chat path and contract

- `backend/Katalogcu.API/Controllers/ChatController.cs`
- `backend/Katalogcu.API/Services/ChatStreamProxyService.cs`
- `backend/Katalogcu.API/Services/ChatStreamEventContract.cs`
- `backend/Katalogcu.API/Contracts/Chat/AiChatRequestWithHistoryDto.cs`
- `backend/Katalogcu.Application/Features/Chat/**`
- `backend/Katalogcu.Application/Common/Interfaces/IChatQueryService.cs`
- `backend/Katalogcu.Infrastructure/Repositories/ChatQueryService.cs`

### Database and migrations

- `backend/Katalogcu.Infrastructure/Migrations/20260526094000_AddCatalogItemSearchText.cs`
- `backend/Katalogcu.Infrastructure/Migrations/20260528090000_AddCatalogItemsEmbeddingHnswIndex.cs`
- `backend/Katalogcu.Infrastructure/Migrations/20260528103000_AddPolicyThresholds.cs`
- `backend/Katalogcu.Infrastructure/Migrations/20260606214500_AddAiCapacityLeases.cs`
- `backend/Katalogcu.Infrastructure/Migrations/20260613075733_SyncAiServiceProductionModel.cs`
- `backend/Katalogcu.Infrastructure/Migrations/20260613075733_SyncAiServiceProductionModel.Designer.cs`
- `backend/Katalogcu.Infrastructure/Migrations/AppDbContextModelSnapshot.cs`
- `backend/Katalogcu.Domain/Entities/PolicyThreshold.cs`
- `backend/Katalogcu.Infrastructure/Repositories/PolicyThresholdRepository.cs`
- `backend/Katalogcu.Application/Common/Interfaces/IPolicyThresholdRepository.cs`

### Docker, smoke, deploy

- `backend/docker-compose.yml`
- `backend/docker-compose.chat-local.yml`
- `backend/scripts/smoke_chat_prod_readiness.sh`
- `backend/cloudbuild.api.yaml`
- `deploy/google-cloud/catalog-only-cloud-run.md`

### Python AI chat service

- `partalog-ai/Dockerfile.chat`
- `partalog-ai/requirements.chat.txt`
- `partalog-ai/.dockerignore`
- `partalog-ai/config.py`
- `partalog-ai/main.py`
- `partalog-ai/api/__init__.py`
- `partalog-ai/api/chat.py`
- `partalog-ai/api/stream_contract.py`
- `partalog-ai/services/ai_capacity.py`
- `partalog-ai/services/chat_context.py`
- `partalog-ai/services/chat_feedback.py`
- `partalog-ai/services/chat_intent.py`
- `partalog-ai/services/chat_matching.py`
- `partalog-ai/services/chat_memory.py`
- `partalog-ai/services/chat_parts.py`
- `partalog-ai/services/chat_policy.py`
- `partalog-ai/services/chat_prompt.py`
- `partalog-ai/services/chat_request.py`
- `partalog-ai/services/chat_responses.py`
- `partalog-ai/services/chat_retrieval.py`
- `partalog-ai/services/chat_sources.py`
- `partalog-ai/services/chat_terms.py`
- `partalog-ai/services/genai_provider.py`
- `partalog-ai/services/policy_thresholds.py`
- `partalog-ai/services/search_text_builder.py`
- `partalog-ai/services/search_trace.py`

### Tests and gates

- `backend/Katalogcu.API.Tests/Services/AesGcmDataProtectionXmlEncryptorTests.cs`
- `backend/Katalogcu.API.Tests/Services/AiCapacityGuardTests.cs`
- `backend/Katalogcu.API.Tests/Services/DistributedPublicChatRateLimiterTests.cs`
- `backend/Katalogcu.API.Tests/Services/ChatStreamProxyServiceTests.cs`
- `backend/Katalogcu.API.Tests/Services/PartalogAiServiceTests.cs`
- `partalog-ai/tests/test_ai_capacity.py`
- `partalog-ai/tests/test_main_health.py`
- `partalog-ai/tests/test_stream_contract.py`
- `.github/workflows/chat-eval-gate.yml`

## Mixed Files That Need Care

These files cannot be blindly staged as-is. They contain prod-chat changes mixed with unrelated work.

- `backend/Katalogcu.API/Program.cs`
  - Prod-chat: DataProtection, Redis rate limit, AI capacity, readiness, migration health.
  - Mixed: external site crawl Hangfire, file storage refactor, broader Serilog/setup changes.
- `backend/Katalogcu.API/Katalogcu.API.csproj`
  - Prod-chat: Redis/Serilog/test-adjacent package support.
  - Mixed: package/project changes may support unrelated features.
- `backend/Katalogcu.Infrastructure/Persistence/AppDbContext.cs`
  - Prod-chat: policy thresholds, capacity leases, search text.
  - Mixed: external matching/crawling/compatibility entities.
- `backend/Katalogcu.Infrastructure/Migrations/AppDbContextModelSnapshot.cs`
  - Mixed by nature; includes all current model changes.
- `backend/docker-compose.yml`
  - Prod-chat: PgBouncer, Redis, API/AI chat settings, DataProtection.
  - Mixed: broader service orchestration changes.
- `partalog-ai/api/chat.py`
  - Prod-chat but large; should be reviewed with the split-out service files.
- `frontend/katalogcu-frontend/src/app/public-view/**`
  - Useful for chat UI/stream state, but should be a separate frontend PR unless needed for backend prod chat launch.

## Exclude From Prod Chat Package

These should not go into the first prod-chat backend release package.

- `backend/Katalogcu.API/wwwroot/static/visual-parts/**` deleted files.
- `embed-test-*.html` deleted files.
- `public-chat-redesign-*.png`
- `login-console.md`, `login-desktop.png`, `login-mobile.png`
- `.playwright-mcp/**`
- Most `frontend/katalogcu-frontend/**` redesign files.
- External matching/crawling files:
  - `backend/Katalogcu.API/Controllers/External*`
  - `backend/Katalogcu.Application/Features/External*`
  - `backend/Katalogcu.Infrastructure/Services/External*`
  - `backend/Katalogcu.Domain/Entities/External*`
- Compatibility/customer-machine feature files unless a later release explicitly includes them.

## Recommended Split Strategy

### Step 1: Do not stage yet

Current worktree is too mixed for `git add .` or broad staging. That would pull unrelated frontend/static/external-matching changes into the prod package.

### Step 2: Build a clean prod-chat branch or patch

Best path:

1. Start from clean `HEAD` in a separate branch/worktree.
2. Reapply only the prod-chat infrastructure and AI chat changes.
3. Recreate or carefully cherry-pick the minimal `Program.cs`, `AppDbContext`, and snapshot changes.
4. Leave frontend redesign/static deletions/external matching out.

Fallback path if staying in this worktree:

1. Use `git add -N` for new candidate files.
2. Use `git add -p` only for mixed files.
3. Avoid staging any `wwwroot/static/visual-parts` deletions.
4. Run the verification gates before commit.

## Verification Gate For The Split Package

Before calling the package deployable:

- `dotnet build backend/Katalogcu.API/Katalogcu.API.csproj --no-restore`
- `dotnet test backend/Katalogcu.API.Tests/Katalogcu.API.Tests.csproj --no-restore --filter AesGcmDataProtectionXmlEncryptorTests`
- Docker build for API and chat AI image.
- `backend/scripts/smoke_chat_prod_readiness.sh --rate-limit-check`
- `/health/ready`
- `/health/migrations`
- Redis DataProtection key-ring contains encrypted XML, not plaintext.

## Decision Needed

The safest next move is to create a clean prod-chat branch/worktree and reapply this manifest. The current dirty tree should remain untouched until the prod-chat package compiles and passes smoke on its own.
