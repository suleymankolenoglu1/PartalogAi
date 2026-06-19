import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { AuthService } from '../../core/services/auth.service';
import { Catalog, CatalogService } from '../../core/services/catalog.service';

type PolicyScopeType = 'Global' | 'Brand' | 'Catalog';

interface PolicyThreshold {
  id: string;
  scopeType: PolicyScopeType;
  scopeKey: string;
  highConfidence: number | null;
  lowConfidence: number | null;
  ambiguityScoreDelta: number | null;
  isActive: boolean;
  version: number;
  notes: string | null;
  updatedBy: string | null;
  createdAt: string;
  updatedAt: string | null;
}

interface PolicyThresholdResponse {
  items: PolicyThreshold[];
}

interface PolicyThresholdForm {
  id: string | null;
  scopeType: PolicyScopeType;
  scopeKey: string;
  highConfidence: number | null;
  lowConfidence: number | null;
  ambiguityScoreDelta: number | null;
  notes: string;
  requireEvaluation: boolean;
}

interface PolicyEvalResult {
  passed: boolean;
  total: number;
  passedCount: number;
  failedCount: number;
  passRate: number;
  thresholdSource: string;
  evaluationToken: string | null;
  results: Array<{
    id: string;
    ok: boolean;
    codes: string[];
    answerPreview: string;
  }>;
}

interface FeedbackEvalDraftResponse {
  success: boolean;
  targetSet: 'behavior' | 'context';
  count: number;
  jsonl: string;
}

interface RegressionPromoteResponse {
  success: boolean;
  appended: number;
  skipped: number;
  requested: number;
  path: string;
  caseIds: string[];
}

interface PolicyOperation {
  id: string;
  action: string;
  title: string;
  actorEmail: string | null;
  actorRole: string | null;
  createdAt: string;
  scopeType: PolicyScopeType | null;
  scopeKey: string | null;
  scopeLabel: string;
  evaluationCaseCount: number | null;
  promotedCaseCount: number | null;
  skippedCaseCount: number | null;
  note: string | null;
}

interface PolicyOperationsResponse {
  items: PolicyOperation[];
}

interface RegressionCasePreview {
  lineNumber: number;
  id: string | null;
  text: string | null;
  message: string | null;
  feedbackId: string | null;
  feedbackReason: string | null;
  catalogIds: string[];
  expectedCodes: string[];
  requiredTerms: string[];
  forbiddenTerms: string[];
  expectNoCodes: boolean;
  hasContext: boolean;
}

interface RegressionCasesResponse {
  items: RegressionCasePreview[];
  total: number;
  path: string;
}

@Component({
  selector: 'app-policy-thresholds',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './policy-thresholds.html',
  styleUrl: './policy-thresholds.css'
})
export class PolicyThresholdsComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly authService = inject(AuthService);
  private readonly catalogService = inject(CatalogService);
  private readonly apiUrl = `${environment.apiUrl}/policy-thresholds`;
  private readonly chatFeedbackApiUrl = `${environment.apiUrl}/chatfeedback`;
  private readonly policyEvalDraftStorageKey = 'partalog.policyThreshold.evalDraftJsonl';

  policies: PolicyThreshold[] = [];
  operations: PolicyOperation[] = [];
  regressionCases: RegressionCasePreview[] = [];
  regressionTotal = 0;
  regressionPath = '';
  catalogs: Catalog[] = [];
  isLoading = false;
  isLoadingOperations = false;
  isLoadingRegressionCases = false;
  isSaving = false;
  includeInactive = false;
  errorMsg: string | null = null;
  successMsg: string | null = null;

  form: PolicyThresholdForm = this.emptyForm();
  evalJsonl = '';
  isEvaluating = false;
  isImportingFeedbackCases = false;
  isPromotingRegressionCases = false;
  evalResult: PolicyEvalResult | null = null;
  evalError: string | null = null;

  get isPlatformAdmin(): boolean {
    return this.authService.isPlatformAdmin();
  }

  ngOnInit(): void {
    this.loadPolicies();
    this.loadOperations();
    this.loadRegressionCases();
    this.loadCatalogs();
    this.loadPendingFeedbackEvalDraft();
  }

  loadPolicies(): void {
    this.isLoading = true;
    this.errorMsg = null;

    const url = `${this.apiUrl}?includeInactive=${this.includeInactive}`;
    this.http.get<PolicyThresholdResponse>(url).subscribe({
      next: (res) => {
        this.policies = Array.isArray(res?.items) ? res.items : [];
        this.isLoading = false;
      },
      error: (err) => {
        console.error('Policy thresholds okunamadı:', err);
        this.errorMsg = 'Policy eşikleri yüklenemedi.';
        this.isLoading = false;
      }
    });
  }

  loadCatalogs(): void {
    this.catalogService.getCatalogs().subscribe({
      next: (catalogs) => {
        this.catalogs = catalogs ?? [];
        if (this.form.scopeType === 'Catalog' && !this.form.scopeKey && this.catalogs.length > 0) {
          this.form.scopeKey = this.catalogs[0].id;
        }
      },
      error: (err) => {
        console.warn('Catalog listesi policy ekranı için yüklenemedi:', err);
      }
    });
  }

  loadOperations(): void {
    this.isLoadingOperations = true;
    this.http.get<PolicyOperationsResponse>(`${this.apiUrl}/operations?take=20`).subscribe({
      next: (res) => {
        this.operations = Array.isArray(res?.items) ? res.items : [];
        this.isLoadingOperations = false;
      },
      error: (err) => {
        console.warn('Policy operasyonları okunamadı:', err);
        this.operations = [];
        this.isLoadingOperations = false;
      }
    });
  }

  loadRegressionCases(): void {
    this.isLoadingRegressionCases = true;
    this.http.get<RegressionCasesResponse>(`${this.apiUrl}/regression-cases?take=12`).subscribe({
      next: (res) => {
        this.regressionCases = Array.isArray(res?.items) ? res.items : [];
        this.regressionTotal = res?.total ?? 0;
        this.regressionPath = res?.path ?? '';
        this.isLoadingRegressionCases = false;
      },
      error: (err) => {
        console.warn('Regression case listesi okunamadı:', err);
        this.regressionCases = [];
        this.regressionTotal = 0;
        this.regressionPath = '';
        this.isLoadingRegressionCases = false;
      }
    });
  }

  onScopeTypeChange(scopeType: PolicyScopeType): void {
    this.form.scopeType = scopeType;
    if (scopeType === 'Global') {
      this.form.scopeKey = 'default';
    } else if (scopeType === 'Catalog') {
      this.form.scopeKey = this.catalogs[0]?.id ?? '';
    } else {
      this.form.scopeKey = '';
    }
  }

  editPolicy(policy: PolicyThreshold): void {
    this.form = {
      id: policy.id,
      scopeType: policy.scopeType,
      scopeKey: policy.scopeKey,
      highConfidence: policy.highConfidence,
      lowConfidence: policy.lowConfidence,
      ambiguityScoreDelta: policy.ambiguityScoreDelta,
      notes: policy.notes ?? '',
      requireEvaluation: true
    };
    this.clearEvalResult();
    this.successMsg = null;
    this.errorMsg = null;
  }

  resetForm(): void {
    this.form = this.emptyForm();
    if (this.form.scopeType === 'Catalog' && this.catalogs.length > 0) {
      this.form.scopeKey = this.catalogs[0].id;
    }
    this.successMsg = null;
    this.errorMsg = null;
    this.clearEvalResult();
  }

  savePolicy(): void {
    const validation = this.validateForm();
    if (validation) {
      this.errorMsg = validation;
      this.successMsg = null;
      return;
    }

    if (this.form.requireEvaluation && !this.evalResult?.passed) {
      this.errorMsg = 'Policy aktifleşmeden önce eval geçmelidir.';
      this.successMsg = null;
      return;
    }

    this.isSaving = true;
    this.errorMsg = null;
    this.successMsg = null;

    const payload = {
      scopeType: this.form.scopeType,
      scopeKey: this.form.scopeType === 'Global' ? 'default' : this.form.scopeKey.trim(),
      highConfidence: this.form.highConfidence,
      lowConfidence: this.form.lowConfidence,
      ambiguityScoreDelta: this.form.ambiguityScoreDelta,
      notes: this.form.notes?.trim() || null,
      requireEvaluation: this.form.requireEvaluation,
      evaluationPassed: !this.form.requireEvaluation || !!this.evalResult?.passed,
      evaluationCaseCount: this.evalResult?.total ?? 0,
      evaluationToken: this.evalResult?.evaluationToken ?? null
    };

    const request = this.form.id
      ? this.http.put<PolicyThreshold>(`${this.apiUrl}/${this.form.id}`, payload)
      : this.http.post<PolicyThreshold>(this.apiUrl, payload);

    request.subscribe({
      next: () => {
        this.successMsg = 'Policy threshold kaydedildi.';
        this.isSaving = false;
        this.resetForm();
        this.loadPolicies();
        this.loadOperations();
      },
      error: (err) => {
        console.error('Policy threshold kaydedilemedi:', err);
        this.errorMsg = err?.error?.message || 'Policy threshold kaydedilemedi.';
        this.isSaving = false;
      }
    });
  }

  evaluatePolicy(): void {
    const validation = this.validateForm();
    if (validation) {
      this.errorMsg = validation;
      this.successMsg = null;
      return;
    }

    let cases: any[];
    try {
      cases = this.parseEvalCases(this.evalJsonl);
    } catch (err) {
      this.evalError = err instanceof Error ? err.message : 'Eval JSONL okunamadı.';
      this.evalResult = null;
      return;
    }

    if (cases.length === 0) {
      this.evalError = 'Eval için en az bir JSONL case gir.';
      this.evalResult = null;
      return;
    }

    this.isEvaluating = true;
    this.evalError = null;
    this.evalResult = null;

    const payload = {
      policy: {
        scopeType: this.form.scopeType,
        scopeKey: this.form.scopeType === 'Global' ? 'default' : this.form.scopeKey.trim(),
        highConfidence: this.form.highConfidence,
        lowConfidence: this.form.lowConfidence,
        ambiguityScoreDelta: this.form.ambiguityScoreDelta,
        notes: this.form.notes?.trim() || null,
        requireEvaluation: false,
        evaluationPassed: false,
        evaluationCaseCount: 0
      },
      cases
    };

    this.http.post<PolicyEvalResult>(`${this.apiUrl}/evaluate`, payload).subscribe({
      next: (res) => {
        this.evalResult = res;
        this.evalError = res.passed ? null : `${res.failedCount} eval case başarısız.`;
        this.isEvaluating = false;
      },
      error: (err) => {
        console.error('Policy eval çalışmadı:', err);
        this.evalError = err?.error?.message || 'Policy eval çalışmadı.';
        this.evalResult = null;
        this.isEvaluating = false;
      }
    });
  }

  appendRecentUnhelpfulFeedbackCases(): void {
    this.isImportingFeedbackCases = true;
    this.evalError = null;

    const catalogIds = this.form.scopeType === 'Catalog' && this.form.scopeKey
      ? [this.form.scopeKey.trim()]
      : [];

    const payload = {
      ids: [],
      targetSet: 'behavior',
      onlyUnhelpful: true,
      useStoredToken: false,
      catalogIds,
      limit: 10
    };

    this.http.post<FeedbackEvalDraftResponse>(`${this.chatFeedbackApiUrl}/eval-case-drafts`, payload).subscribe({
      next: (res) => {
        this.appendEvalJsonl(res.jsonl);
        this.successMsg = `${res.count} son unhelpful feedback eval havuzuna eklendi.`;
        this.errorMsg = null;
        this.isImportingFeedbackCases = false;
      },
      error: (err) => {
        console.error('Feedback eval case import edilemedi:', err);
        this.evalError = err?.error?.message || 'Feedback case eklenemedi.';
        this.isImportingFeedbackCases = false;
      }
    });
  }

  promoteEvalCasesToRegressionSet(): void {
    if (!this.evalJsonl.trim()) {
      this.errorMsg = 'Regression set için önce eval case ekle.';
      this.successMsg = null;
      return;
    }

    if (!this.evalResult?.passed) {
      this.errorMsg = 'Regression set’e eklemeden önce eval geçmelidir.';
      this.successMsg = null;
      return;
    }

    this.isPromotingRegressionCases = true;
    this.errorMsg = null;
    this.successMsg = null;

    const payload = {
      jsonl: this.evalJsonl,
      note: this.form.notes?.trim() || `${this.form.scopeType}:${this.form.scopeKey}`,
      evaluationToken: this.evalResult.evaluationToken
    };

    this.http.post<RegressionPromoteResponse>(`${this.apiUrl}/regression-cases`, payload).subscribe({
      next: (res) => {
        this.successMsg = `${res.appended} case regression set’e eklendi, ${res.skipped} duplicate atlandı.`;
        this.errorMsg = null;
        this.isPromotingRegressionCases = false;
        this.loadOperations();
        this.loadRegressionCases();
      },
      error: (err) => {
        console.error('Regression case promote edilemedi:', err);
        this.errorMsg = err?.error?.message || 'Regression case promote edilemedi.';
        this.successMsg = null;
        this.isPromotingRegressionCases = false;
      }
    });
  }

  deactivatePolicy(policy: PolicyThreshold): void {
    if (!policy.isActive) return;
    this.http.post<PolicyThreshold>(`${this.apiUrl}/${policy.id}/deactivate`, {}).subscribe({
      next: () => {
        this.successMsg = 'Policy pasifleştirildi.';
        this.loadPolicies();
        this.loadOperations();
      },
      error: (err) => {
        console.error('Policy threshold pasifleştirilemedi:', err);
        this.errorMsg = err?.error?.message || 'Policy pasifleştirilemedi.';
      }
    });
  }

  activatePolicy(policy: PolicyThreshold): void {
    if (policy.isActive) return;
    this.http.post<PolicyThreshold>(`${this.apiUrl}/${policy.id}/activate`, {}).subscribe({
      next: () => {
        this.successMsg = 'Policy versiyonu yeniden aktif edildi.';
        this.errorMsg = null;
        this.loadPolicies();
        this.loadOperations();
      },
      error: (err) => {
        console.error('Policy threshold aktifleştirilemedi:', err);
        this.errorMsg = err?.error?.message || 'Policy aktifleştirilemedi.';
        this.successMsg = null;
      }
    });
  }

  scopeLabel(policy: PolicyThreshold): string {
    if (policy.scopeType === 'Global') return 'Global';
    if (policy.scopeType === 'Brand') return `Marka: ${policy.scopeKey}`;
    const catalog = this.catalogs.find(x => x.id === policy.scopeKey);
    return `Katalog: ${catalog?.name ?? policy.scopeKey}`;
  }

  formatThreshold(value: number | null): string {
    return typeof value === 'number' ? value.toFixed(2) : '-';
  }

  activeSourcePreview(policy: PolicyThreshold): string {
    if (policy.scopeType === 'Global') return 'db:global';
    if (policy.scopeType === 'Brand') return `db:brand:${policy.scopeKey}`;
    return `db:catalog:${policy.scopeKey}`;
  }

  operationMeta(operation: PolicyOperation): string {
    const pieces: string[] = [];
    if (operation.evaluationCaseCount !== null && operation.evaluationCaseCount !== undefined) {
      pieces.push(`${operation.evaluationCaseCount} eval case`);
    }
    if (operation.promotedCaseCount !== null && operation.promotedCaseCount !== undefined) {
      pieces.push(`${operation.promotedCaseCount} promoted`);
    }
    if (operation.skippedCaseCount !== null && operation.skippedCaseCount !== undefined && operation.skippedCaseCount > 0) {
      pieces.push(`${operation.skippedCaseCount} duplicate`);
    }
    if (operation.note) {
      pieces.push(operation.note);
    }
    return pieces.join(' | ');
  }

  regressionCaseAssertionLabel(item: RegressionCasePreview): string {
    if (item.expectNoCodes) return 'No-code';
    if (item.expectedCodes.length) return `Expected: ${item.expectedCodes.join(', ')}`;
    if (item.forbiddenTerms.length) return `Forbidden: ${item.forbiddenTerms.join(', ')}`;
    if (item.requiredTerms.length) return `Required: ${item.requiredTerms.join(', ')}`;
    return 'Assertion yok';
  }

  onPolicyInputChange(): void {
    this.clearEvalResult();
  }

  private validateForm(): string | null {
    if ((this.form.scopeType === 'Global' || this.form.scopeType === 'Brand') && !this.isPlatformAdmin) {
      return 'Global ve marka policy sadece platform admin tarafından yönetilebilir.';
    }

    if (this.form.scopeType !== 'Global' && !this.form.scopeKey.trim()) {
      return 'Scope key zorunludur.';
    }

    const values = [this.form.highConfidence, this.form.lowConfidence, this.form.ambiguityScoreDelta];
    if (values.every(value => value === null || value === undefined)) {
      return 'En az bir threshold değeri gir.';
    }

    if (values.some(value => typeof value === 'number' && (value < 0 || value > 1))) {
      return 'Threshold değerleri 0 ile 1 arasında olmalıdır.';
    }

    if (
      typeof this.form.lowConfidence === 'number' &&
      typeof this.form.highConfidence === 'number' &&
      this.form.lowConfidence > this.form.highConfidence
    ) {
      return 'Low threshold, high threshold değerinden büyük olamaz.';
    }

    return null;
  }

  private emptyForm(): PolicyThresholdForm {
    return {
      id: null,
      scopeType: 'Catalog',
      scopeKey: '',
      highConfidence: 0.85,
      lowConfidence: 0.55,
      ambiguityScoreDelta: 0.10,
      notes: '',
      requireEvaluation: true
    };
  }

  private clearEvalResult(): void {
    this.evalResult = null;
    this.evalError = null;
  }

  private loadPendingFeedbackEvalDraft(): void {
    const draft = window.localStorage.getItem(this.policyEvalDraftStorageKey);
    if (!draft?.trim()) return;

    this.appendEvalJsonl(draft);
    window.localStorage.removeItem(this.policyEvalDraftStorageKey);
    this.successMsg = 'Chat kalite panelinden aktarılan feedback eval caseleri eklendi.';
  }

  private appendEvalJsonl(jsonl: string | null | undefined): void {
    const incoming = (jsonl ?? '').trim();
    if (!incoming) return;

    const current = this.evalJsonl.trim();
    this.evalJsonl = current ? `${current}\n${incoming}\n` : `${incoming}\n`;
    this.clearEvalResult();
  }

  private parseEvalCases(jsonl: string): any[] {
    return jsonl
      .split('\n')
      .map(line => line.trim())
      .filter(line => line.length > 0 && !line.startsWith('#'))
      .map((line, index) => {
        try {
          const raw = JSON.parse(line);
          return {
            id: raw.id ?? `case-${index + 1}`,
            text: raw.text ?? raw.message,
            message: raw.message,
            catalogIds: raw.catalog_ids ?? raw.catalogIds ?? [],
            contextJson: raw.context_json ?? raw.contextJson ?? raw.context,
            expectedCodes: raw.expected_codes ?? raw.expectedCodes ?? [],
            requiredTerms: raw.required_terms ?? raw.requiredTerms ?? [],
            forbiddenTerms: raw.forbidden_terms ?? raw.forbiddenTerms ?? [],
            expectNoCodes: !!(raw.expect_no_codes ?? raw.expectNoCodes)
          };
        } catch {
          throw new Error(`${index + 1}. satır geçerli JSON değil.`);
        }
      });
  }
}
