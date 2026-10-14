import type { TeamStats } from '../types'
import type { View } from '../view'

type Row = [label: string, pick: (t: TeamStats) => number, format: (v: number) => string]

const ANALYST_ROWS: Row[] = [
  ['Possession', (t) => t.possession, (v) => `${Math.round(v)}%`],
  ['xG', (t) => t.xg, (v) => v.toFixed(2)],
  ['Shots', (t) => t.shots, String],
  ['On target', (t) => t.shotsOnTarget, String],
  ['Pass accuracy', (t) => t.passAccuracy, (v) => `${Math.round(v)}%`],
  ['Progressive passes', (t) => t.progressivePasses, String],
  ['Pressures', (t) => t.pressures, String],
  ['Tackles won', (t) => t.tackles, String],
  ['Interceptions', (t) => t.interceptions, String],
]

const SIMPLE_ROWS: Row[] = [
  ['Possession', (t) => t.possession, (v) => `${Math.round(v)}%`],
  ['Shots', (t) => t.shots, String],
]

/** Team comparison. Club fans see their team on the left as "Us". */
export function Meters({ view }: { view: View }) {
  const { info, snapshot, mode, club } = view
  if (!snapshot) return null

  const flip = mode === 'club' && club?.teamId === info.away.teamId
  const [left, right] = flip ? [snapshot.away, snapshot.home] : [snapshot.home, snapshot.away]
  const [leftName, rightName] = mode === 'club' && club ? ['Us', 'Them'] : [info.home.name, info.away.name]
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
              <span className="h">{fmt(h)}</span>
              <span className="label">{label}</span>
              <span className="a">{fmt(a)}</span>
              <div className="bar">
                <div style={{ width: `${h + a === 0 ? 50 : (100 * h) / (h + a)}%` }} />
              </div>
            </div>
          )
        })}
      </div>

      {mode !== 'casual' && (
        <>
          <Gauge left={rightName} title="Momentum" right={leftName} value={50 + momentum / 2} className="momentum" />
          <Gauge left="Control" title="Game state" right="Chaos" value={snapshot.chaos} className="chaos" />
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
