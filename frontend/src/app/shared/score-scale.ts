import { Pipe, PipeTransform } from '@angular/core';

/**
 * D-UI-1 — the single published score scale, client side.
 *
 * Mirrors `AlFalah.Application.Analysis.ScoreScale` exactly. The API returns
 * averages on the rubric's internal 0–4 scale (the performance-level thresholds
 * and the immutable visit snapshots are defined on it), and every human-facing
 * surface publishes them on **0–100**.
 *
 * Before this, the same result was rendered three ways — `78 / 100`,
 * `3.12 / 4`, `91.7%` — sometimes in adjacent cells of one card, so two numbers
 * that agreed looked like they disagreed.
 *
 * The one documented exception is an individual standard's score: that is the
 * rubric LEVEL (0–4) and is always shown beside its Arabic word ("4 متميز").
 */
export const RAW_MAXIMUM = 4;
export const PUBLISHED_MAXIMUM = 100;

/** Converts a raw 0–4 average to the published 0–100 scale (1 decimal). */
export function toPublishedScore(rawOutOfFour: number | null | undefined): number | null {
  if (rawOutOfFour === null || rawOutOfFour === undefined || Number.isNaN(rawOutOfFour)) return null;
  const clamped = Math.min(Math.max(rawOutOfFour, 0), RAW_MAXIMUM);
  return Math.round(clamped * (PUBLISHED_MAXIMUM / RAW_MAXIMUM) * 10) / 10;
}

/** Published figure as text, with no trailing ".0" on a whole number. */
export function formatPublishedScore(rawOutOfFour: number | null | undefined, dash = '—'): string {
  const published = toPublishedScore(rawOutOfFour);
  return published === null ? dash : String(published);
}

/** Published figure with its scale stated: "91.7 / 100". */
export function formatPublishedScoreWithMaximum(rawOutOfFour: number | null | undefined, dash = '—'): string {
  const published = toPublishedScore(rawOutOfFour);
  return published === null ? dash : `${published} / ${PUBLISHED_MAXIMUM}`;
}

/**
 * Normalises an already-published points pair (snapshot `totalScore` /
 * `maximumScore`) onto 0–100 so the denominator is always the published one,
 * even for older snapshots that stored raw points.
 */
export function formatPublishedTotal(total: number | null | undefined, maximum: number | null | undefined): string {
  if (total === null || total === undefined || !maximum || maximum <= 0) return `— / ${PUBLISHED_MAXIMUM}`;
  const ratio = Math.min(Math.max(total / maximum, 0), 1);
  const published = Math.round(ratio * PUBLISHED_MAXIMUM * 10) / 10;
  return `${published} / ${PUBLISHED_MAXIMUM}`;
}

/**
 * Signed delta between two raw 0–4 averages, published on the 0–100 scale.
 * Not clamped: a difference can legitimately be negative.
 */
export function toPublishedDelta(rawDelta: number | null | undefined): number | null {
  if (rawDelta === null || rawDelta === undefined || Number.isNaN(rawDelta)) return null;
  return Math.round(rawDelta * (PUBLISHED_MAXIMUM / RAW_MAXIMUM) * 10) / 10;
}

/** Percentage of the published maximum, for meters and bar widths. */
export function publishedScorePercent(rawOutOfFour: number | null | undefined): number {
  const published = toPublishedScore(rawOutOfFour);
  return published === null ? 0 : published;
}

/**
 * `{{ value | publishedScore }}` → "91.7"
 * `{{ value | publishedScore:'withMax' }}` → "91.7 / 100"
 *
 * A pipe rather than per-component formatting so no template can reintroduce a
 * second scale.
 */
@Pipe({ name: 'publishedScore', standalone: true })
export class PublishedScorePipe implements PipeTransform {
  transform(value: number | null | undefined, mode: 'plain' | 'withMax' = 'plain'): string {
    return mode === 'withMax'
      ? formatPublishedScoreWithMaximum(value)
      : formatPublishedScore(value);
  }
}

/**
 * Signed delta on the published scale: "+8.5" / "−12.5". Used by the teacher
 * progress table, where a raw "+0.34" was meaningless next to /100 columns.
 */
@Pipe({ name: 'publishedScoreDelta', standalone: true })
export class PublishedScoreDeltaPipe implements PipeTransform {
  transform(rawDelta: number | null | undefined, dash = '—'): string {
    const published = toPublishedDelta(rawDelta);
    if (published === null) return dash;
    const sign = published > 0 ? '+' : '';
    return `${sign}${published}`;
  }
}
