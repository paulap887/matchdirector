import { clock, LABELS, reviewOf, type Overlay } from '../types'
import { isOurs, lineFor, visible, type View } from '../view'
import { Chips, Insight, ReviewBadge } from './OverlayCard'

export function Feed({ overlays, view }: { overlays: Overlay[]; view: View }) {
  const shown = overlays.filter((o) => visible(o, view))
  const title = view.mode === 'player' && view.player ? `${view.player.name}'s moments` : view.mode === 'casual' ? 'Big moments' : 'Moments'

  return (
    <aside className="feed">
      <h2>{title}</h2>
      {shown.length === 0 && <p className="empty">Nothing yet. Moments appear here as they happen.</p>}
      <ol>
        {shown.map((o) => {
          const ours = isOurs(o, view)
          return (
            <li key={o.overlayId} className={`t-${o.type} ${ours === false ? 'theirs' : ''}`}>
              <span className="time">{clock(o.matchClock)}</span>
              <span className="tag">{LABELS[o.type] ?? o.type}</span>
              <p>{lineFor(o, view)}</p>
              {view.mode === 'analyst' && (
                <>
                  <Chips metrics={o.metrics} />
                  {o.variants.insight && <Insight text={o.variants.insight} />}
                  <ReviewBadge review={reviewOf(o)} />
                </>
              )}
            </li>
          )
        })}
      </ol>
    </aside>
  )
}
