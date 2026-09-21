import { applyQuickScore, liveTotals, suggestedScore } from './visit-v2-calculator';

describe('Visit V2 calculator', () => {
  it('matches the prototype indicator suggestions', () => {
    expect(suggestedScore(0, 3)).toBe(1);
    expect(suggestedScore(1, 3)).toBe(2);
    expect(suggestedScore(2, 3)).toBe(3);
    expect(suggestedScore(1, 2)).toBe(3);
    expect(suggestedScore(3, 3)).toBe(4);
  });

  it('quick score selects the prototype number of indicators', () => {
    const standard: any = { score: 1, indicators: [1, 2, 3].map(id => ({ id, isObserved: false })) };
    expect(applyQuickScore(standard, 3).indicators.filter(i => i.isObserved).length).toBe(2);
    expect(applyQuickScore(standard, 4).indicators.every(i => i.isObserved)).toBeTrue();
  });

  it('starts a 25-standard card at 25 of 100', () => {
    const domains: any = [{ standards: Array.from({ length: 25 }, () => ({ score: 1 })) }];
    expect(liveTotals(domains)).toEqual({ total: 25, maximum: 100, percentage: 25 });
  });
});
