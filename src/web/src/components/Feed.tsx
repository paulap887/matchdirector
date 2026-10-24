import { momentLabel, t, translated } from '../i18n'
import { clock, reviewOf, type Overlay } from '../types'
import { isOurs, lineFor, visible, type View } from '../view'
import { Alert, Chips, Insight, ReviewBadge, Text } from './OverlayCard'

export function Feed({ overlays, view }: { overlays: Overlay[]; view: View }) {
  const { lang } = view
  const shown = overlays.filter((o) => visible(o, view))
  const title =
    view.mode === 'player' && view.player ? t(lang, 'feed.player', { name: view.player.name }) : t(lang, view.mode === 'casual' ? 'feed.big' : 'feed.all')

  return (
    <aside className="feed">
      <h2>{title}</h2>
      {shown.length === 0 && <p className="empty">{t(lang, 'feed.empty')}</p>}
      <ol>
        {shown.map((o) => {
          const ours = isOurs(o, view)
          const insight = translated(o.variants, 'insight', lang)
          return (
            <li key={o.overlayId} className={`t-${o.type} ${ours === false ? 'theirs' : ''}`}>
              <span className="time">{clock(o.matchClock)}</span>
              <span className="tag">{momentLabel(lang, o.type)}</span>
              <p>
                <Text line={lineFor(o.variants, view)} lang={lang} />
              </p>
              <Alert overlay={o} lang={lang} />
              {view.mode === 'analyst' && (
                <>
                  <Chips metrics={o.metrics} />
                  {insight && <Insight line={insight} lang={lang} />}
                  <ReviewBadge review={reviewOf(o)} lang={lang} />
                </>
              )}
            </li>
          )
        })}
      </ol>
    </aside>
  )
}
