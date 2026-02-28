export type PlanId = 1 | 2 | 3;

export function normalizePlan(value: unknown): PlanId {
  const n = Number(value);
  if (n >= 3) return 3;
  if (n >= 2) return 2;
  return 1;
}

export function planFromRaw(value: unknown): PlanId | null {
  if (value === 'CatalogWithAIAndEcommerce' || value === 'Catalog + AI + E-Ticaret' || value === 3 || value === '3') return 3;
  if (value === 'CatalogWithAI' || value === 'Catalog + AI' || value === 2 || value === '2') return 2;
  if (value === 'CatalogOnly' || value === 'Catalog' || value === 'Katalog' || value === 1 || value === '1') return 1;
  return null;
}

export function getPlanCodeName(plan: PlanId): string {
  switch (plan) {
    case 3: return 'CatalogWithAIAndEcommerce';
    case 2: return 'CatalogWithAI';
    default: return 'CatalogOnly';
  }
}

export function getPlanDisplayName(plan: PlanId): string {
  switch (plan) {
    case 3: return 'Catalog + AI + E-Ticaret';
    case 2: return 'Catalog + AI';
    default: return 'Katalog';
  }
}
