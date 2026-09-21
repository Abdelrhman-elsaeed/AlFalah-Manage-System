import { VisitV2Domain, VisitV2Standard } from '../../core/models/visit-v2.models';

export function suggestedScore(observedCount: number, indicatorCount: number): number {
  if (observedCount <= 0 || indicatorCount <= 0) return 1;
  if (observedCount >= indicatorCount) return 4;
  return indicatorCount === 2 ? 3 : Math.min(3, observedCount + 1);
}

export function applyQuickScore(standard: VisitV2Standard, score: number): VisitV2Standard {
  const observedCount = score === 4 ? standard.indicators.length : score === 3 ? Math.min(2, standard.indicators.length) : score === 2 ? Math.min(1, standard.indicators.length) : 0;
  return {
    ...standard,
    score,
    indicators: standard.indicators.map((indicator, index) => ({ ...indicator, isObserved: index < observedCount }))
  };
}

export function liveTotals(domains: VisitV2Domain[]): { total: number; maximum: number; percentage: number } {
  const standards = domains.flatMap(domain => domain.standards);
  const total = standards.reduce((sum, standard) => sum + standard.score, 0);
  const maximum = standards.length * 4;
  return { total, maximum, percentage: maximum ? Math.round(total / maximum * 100) : 0 };
}

