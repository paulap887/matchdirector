import type { MetricKey } from './types'
import type { View } from './view'

/** Stats a viewer can pin next to the scorebug. */
export const METRICS: Record<MetricKey, { label: string; home: (v: View) => number; away: (v: View) => number; format: (n: number) => string; single?: boolean }> = {
  possession: { label: 'Possession', home: (v) => v.snapshot!.home.possession, away: (v) => v.snapshot!.away.possession, format: (n) => `${Math.round(n)}%` },
  xg: { label: 'xG', home: (v) => v.snapshot!.home.xg, away: (v) => v.snapshot!.away.xg, format: (n) => n.toFixed(2) },
  shots: { label: 'Shots', home: (v) => v.snapshot!.home.shots, away: (v) => v.snapshot!.away.shots, format: String },
  passAccuracy: { label: 'Pass accuracy', home: (v) => v.snapshot!.home.passAccuracy, away: (v) => v.snapshot!.away.passAccuracy, format: (n) => `${Math.round(n)}%` },
  progressivePasses: { label: 'Progressive passes', home: (v) => v.snapshot!.home.progressivePasses, away: (v) => v.snapshot!.away.progressivePasses, format: String },
  momentum: { label: 'Momentum', home: (v) => v.snapshot!.momentum, away: () => 0, format: (n) => (n > 0 ? `+${Math.round(n)}` : `${Math.round(n)}`), single: true },
  chaos: { label: 'Chaos index', home: (v) => v.snapshot!.chaos, away: () => 0, format: (n) => `${Math.round(n)}/100`, single: true },
}

export const METRIC_OPTIONS = Object.entries(METRICS).map(([key, m]) => ({ key: key as MetricKey, label: m.label }))
