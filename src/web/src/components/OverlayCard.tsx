import { useMemo, useState } from 'react'
import { clock, LABELS, reviewOf, type AgentStep, type Overlay } from '../types'
import { isOurs, lineFor, type View } from '../view'

export function OverlayCard({ overlay, view }: { overlay: Overlay; view: View }) {
  const [why, setWhy] = useState(view.mode === 'analyst')
  const player = useMemo(
    () => [...view.info.home.players, ...view.info.away.players].find((p) => p.playerId === overlay.playerId),
    [view.info, overlay.playerId],
  )
  const ours = isOurs(overlay, view)
  const review = reviewOf(overlay)
  const insight = overlay.variants.insight

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
        <ReviewBadge review={review} />
      </div>
      <p className="line">{lineFor(overlay, view)}</p>
      {view.mode === 'analyst' && <Chips metrics={overlay.metrics} />}
      {insight && (view.mode === 'analyst' || why) && <Insight text={insight} />}
      <button className="why" onClick={() => setWhy(!why)} aria-expanded={why}>
        {why ? 'Hide evidence' : 'Why am I seeing this?'}
      </button>
      {why && (
        <div className="evidence">
          {overlay.trace && <Trace steps={overlay.trace} />}
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

export function ReviewBadge({ review }: { review: ReturnType<typeof reviewOf> }) {
  if (review === 'none') return null
  const text = { pending: '⏳ fact-checking…', approved: '✓ fact-checked', corrected: '✎ corrected by verifier' }[review]
  return <span className={`review ${review}`}>{text}</span>
}

export function Insight({ text }: { text: string }) {
  return (
    <p className="insight">
      <b>Why it matters</b> {text}
    </p>
  )
}

/** How this overlay was made: each agent and code check, in order. */
export function Trace({ steps }: { steps: AgentStep[] }) {
  return (
    <ol className="trace" aria-label="How this was made">
      {steps.map((s, i) => (
        <li key={i} className={s.agent === 'Number check' || s.model === 'code' ? 'code' : 'agent'}>
          <span className="who">
            {s.agent}
            <small>{s.model === 'code' ? 'deterministic code' : s.model}</small>
          </span>
          <span className="what">
            {s.outcome}
            {s.durationMs > 0 && <small> · {(s.durationMs / 1000).toFixed(1)} s</small>}
            {s.detail && <em>{s.detail}</em>}
          </span>
        </li>
      ))}
    </ol>
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
