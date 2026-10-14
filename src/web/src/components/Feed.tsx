import { clock, LABELS, type Overlay } from '../types'
import { lineFor, teamOf, visible, type View } from '../view'
import { Chips } from './OverlayCard'

export function Feed({ overlays, view }: { overlays: Overlay[]; view: View }) {
  const shown = overlays.filter((o) => visible(o, view))
  const title = view.mode === 'player' && view.player ? `${view.player.name}'s moments` : view.mode === 'casual' ? 'Big moments' : 'Moments'

  return (
    <aside className="feed">
      <h2>{title}</h2>
      {shown.length === 0 && <p className="empty">Nothing yet. Moments appear here as they happen.</p>}
      <ol>
        {shown.map((o) => {
          const ours = view.mode === 'club' && view.club ? teamOf(o, view.info)?.teamId === view.club.teamId : undefined
          return (
            <li key={o.overlayId} className={`t-${o.type} ${ours === false ? 'theirs' : ''}`}>
              <span className="time">{clock(o.matchClock)}</span>
              <span className="tag">{LABELS[o.type] ?? o.type}</span>
              <p>{lineFor(o, view)}</p>
              {view.mode === 'analyst' && <Chips metrics={o.metrics} />}
            </li>
          )
        })}
      </ol>
    </aside>
  )
}
