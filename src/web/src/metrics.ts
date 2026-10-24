import { num, pct, type Key, type Lang } from './i18n'
import type { MetricKey } from './types'
import type { View } from './view'

type Metric = { label: Key; home: (v: View) => number; away: (v: View) => number; format: (n: number, lang: Lang) => string; single?: boolean }

const share = (n: number, lang: Lang) => pct(lang, n)

/** Stats a viewer can pin next to the scorebug. */
export const METRICS: Record<MetricKey, Metric> = {
  possession: { label: 'stat.possession', home: (v) => v.snapshot!.home.possession, away: (v) => v.snapshot!.away.possession, format: share },
  xg: { label: 'stat.xg', home: (v) => v.snapshot!.home.xg, away: (v) => v.snapshot!.away.xg, format: (n, lang) => num(lang, n, 2) },
  shots: { label: 'stat.shots', home: (v) => v.snapshot!.home.shots, away: (v) => v.snapshot!.away.shots, format: (n, lang) => num(lang, n) },
  passAccuracy: { label: 'stat.passAccuracy', home: (v) => v.snapshot!.home.passAccuracy, away: (v) => v.snapshot!.away.passAccuracy, format: share },
  progressivePasses: { label: 'stat.progressive', home: (v) => v.snapshot!.home.progressivePasses, away: (v) => v.snapshot!.away.progressivePasses, format: (n, lang) => num(lang, n) },
  momentum: { label: 'stat.momentum', home: (v) => v.snapshot!.momentum, away: () => 0, format: (n, lang) => (n > 0 ? `+${num(lang, n)}` : num(lang, n)), single: true },
  chaos: { label: 'stat.chaos', home: (v) => v.snapshot!.chaos, away: () => 0, format: (n, lang) => `${num(lang, n)}/100`, single: true },
}

export const METRIC_OPTIONS = Object.entries(METRICS).map(([key, m]) => ({ key: key as MetricKey, label: m.label }))
