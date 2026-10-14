import { useMemo, useState } from 'react'
import { clock, LABELS, type Overlay } from '../types'
import { lineFor, teamOf, type View } from '../view'

export function OverlayCard({ overlay, view }: { overlay: Overlay; view: View }) {
  const [why, setWhy] = useState(view.mode === 'analyst')
  const player = useMemo(
    () => [...view.info.home.players, ...view.info.away.players].find((p) => p.playerId === overlay.playerId),
    [view.info, overlay.playerId],
  )
  const team = teamOf(overlay, view.info)
  const ours = view.mode === 'club' && view.club ? team?.teamId === view.club.teamId : undefined
  const source = overlay.variants.source === 'agent' ? 'AI narrated · every number verified by code' : 'Template narration (deterministic)'

  return (
    <article className={`card t-${overlay.type} ${ours === false ? 'theirs' : ''} ${view.mode}`} aria-live="polite">
      <div className="card-head">
        <span className="tag">{LABELS[overlay.type] ?? overlay.type}</span>
        <span className="time">{clock(overlay.matchClock)}</span>
        {player && (
          <span className="player">
            #{player.number} {player.name}
          </span>
        )}
      </div>
      <p className="line">{lineFor(overlay, view)}</p>
      {view.mode === 'analyst' && <Chips metrics={overlay.metrics} />}
      <button className="why" onClick={() => setWhy(!why)} aria-expanded={why}>
        {why ? 'Hide evidence' : 'Why am I seeing this?'}
      </button>
      {why && (
        <div className="evidence">
          <p className="source">{source}</p>
          <table>
            <tbody>
              {Object.entries(overlay.metrics).map(([k, v]) => (
                <tr key={k}>
                  <th>{k}</th>
                  <td>{v}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <p className="cites">
            Computed from events {overlay.evidence.map((id) => <code key={id}>#{Number(id.split('-').pop())}</code>)}
          </p>
        </div>
      )}
    </article>
  )
}

export function Chips({ metrics }: { metrics: Record<string, number> }) {
  return (
    <div className="chips">
      {Object.entries(metrics)
        .slice(0, 5)
        .map(([k, v]) => (
          <span key={k}>
            {k} <b>{v}</b>
          </span>
        ))}
    </div>
  )
}
