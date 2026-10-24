import { num, pct, t, type Key, type Lang } from '../i18n'
import type { TeamStats } from '../types'
import type { View } from '../view'

type Row = [label: Key, pick: (t: TeamStats) => number, format: (v: number, lang: Lang) => string]

const count = (v: number, lang: Lang) => num(lang, v)
const share = (v: number, lang: Lang) => pct(lang, v)

const ANALYST_ROWS: Row[] = [
  ['stat.possession', (t) => t.possession, share],
  ['stat.xg', (t) => t.xg, (v, lang) => num(lang, v, 2)],
  ['stat.shots', (t) => t.shots, count],
  ['stat.onTarget', (t) => t.shotsOnTarget, count],
  ['stat.passAccuracy', (t) => t.passAccuracy, share],
  ['stat.progressive', (t) => t.progressivePasses, count],
  ['stat.pressures', (t) => t.pressures, count],
  ['stat.tackles', (t) => t.tackles, count],
  ['stat.interceptions', (t) => t.interceptions, count],
]

const SIMPLE_ROWS: Row[] = [
  ['stat.possession', (t) => t.possession, share],
  ['stat.shots', (t) => t.shots, count],
]

/** Team comparison. Club fans see their team on the left as "Us". */
export function Meters({ view }: { view: View }) {
  const { info, snapshot, mode, club, lang } = view
  if (!snapshot) return null

  const flip = mode === 'club' && club?.teamId === info.away.teamId
  const [left, right] = flip ? [snapshot.away, snapshot.home] : [snapshot.home, snapshot.away]
  const [leftName, rightName] = mode === 'club' && club ? [t(lang, 'us'), t(lang, 'them')] : [info.home.name, info.away.name]
  const rows = mode === 'analyst' ? ANALYST_ROWS : SIMPLE_ROWS
  const momentum = flip ? -snapshot.momentum : snapshot.momentum

  return (
    <div className={`meters ${flip ? 'flip' : ''}`}>
      <div className="stats">
        <div className="stat head">
          <span className="h">{leftName}</span>
          <span />
          <span className="a">{rightName}</span>
        </div>
        {rows.map(([label, pick, fmt]) => {
          const h = pick(left)
          const a = pick(right)
          return (
            <div className="stat" key={label}>
              <span className="h">{fmt(h, lang)}</span>
              <span className="label">{t(lang, label)}</span>
              <span className="a">{fmt(a, lang)}</span>
              <div className="bar">
                <div style={{ width: `${h + a === 0 ? 50 : (100 * h) / (h + a)}%` }} />
              </div>
            </div>
          )
        })}
      </div>

      {mode !== 'casual' && (
        <>
          <Gauge left={rightName} title={t(lang, 'stat.momentum')} right={leftName} value={50 + momentum / 2} className="momentum" />
          <Gauge left={t(lang, 'control')} title={t(lang, 'gameState')} right={t(lang, 'chaos')} value={snapshot.chaos} className="chaos" />
        </>
      )}
    </div>
  )
}

function Gauge({ left, title, right, value, className }: { left: string; title: string; right: string; value: number; className: string }) {
  return (
    <div className="gauge">
      <div className="gauge-label">
        <span>{left}</span>
        <strong>{title}</strong>
        <span>{right}</span>
      </div>
      <div className={`track ${className}`} role="meter" aria-label={title} aria-valuenow={Math.round(value)} aria-valuemin={0} aria-valuemax={100}>
        <div className="needle" style={{ left: `${Math.min(100, Math.max(0, value))}%` }} />
      </div>
    </div>
  )
}
