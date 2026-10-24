import { useMemo, useState } from 'react'
import { momentLabel, num, t, translated, type Lang } from '../i18n'
import { clock, reviewOf, type AgentStep, type Overlay } from '../types'
import { isOurs, lineFor, type Line, type View } from '../view'

export function OverlayCard({ overlay, view }: { overlay: Overlay; view: View }) {
  const { lang } = view
  const [why, setWhy] = useState(view.mode === 'analyst')
  const player = useMemo(
    () => [...view.info.home.players, ...view.info.away.players].find((p) => p.playerId === overlay.playerId),
    [view.info, overlay.playerId],
  )
  const ours = isOurs(overlay, view)
  const review = reviewOf(overlay)
  const insight = translated(overlay.variants, 'insight', lang)

  return (
    <article className={`card t-${overlay.type} ${ours === false ? 'theirs' : ''} ${view.mode}`} aria-live="polite">
      <div className="card-head">
        <span className="tag">{momentLabel(lang, overlay.type)}</span>
        <span className="time">{clock(overlay.matchClock)}</span>
        {player && (
          <span className="player">
            #{player.number} {player.name}
          </span>
        )}
        <Alert overlay={overlay} lang={lang} />
        <ReviewBadge review={review} lang={lang} />
      </div>
      <p className="line">
        <Text line={lineFor(overlay.variants, view)} lang={lang} />
      </p>
      {view.mode === 'analyst' && <Chips metrics={overlay.metrics} />}
      {insight && (view.mode === 'analyst' || why) && <Insight line={insight} lang={lang} />}
      <button className="why" onClick={() => setWhy(!why)} aria-expanded={why}>
        {t(lang, why ? 'hideEvidence' : 'why')}
      </button>
      {why && (
        <div className="evidence">
          {overlay.trace && <Trace steps={overlay.trace} lang={lang} />}
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
            {t(lang, 'computedFrom')} {overlay.evidence.map((id) => <code key={id}>#{Number(id.split('-').pop())}</code>)}
          </p>
        </div>
      )}
    </article>
  )
}

/** A studio line, tagged "EN" when it is shown in English to a viewer reading another language. */
export function Text({ line, lang }: { line: Line; lang: Lang }) {
  return (
    <>
      {line.text}
      {line.english && (
        <abbr className="en" title={t(lang, 'notTranslated')}>
          EN
        </abbr>
      )}
    </>
  )
}

/** Speed indicator, shown when a moment crossed one of the stats engine's speed thresholds. */
export function Alert({ overlay, lang }: { overlay: Overlay; lang: Lang }) {
  const threshold = overlay.metrics.speedThreshold
  if (threshold === undefined) return null
  return <span className="alert">{t(lang, 'speedAlert', { v: num(lang, threshold, Number.isInteger(threshold) ? 0 : 1) })}</span>
}

export function ReviewBadge({ review, lang }: { review: ReturnType<typeof reviewOf>; lang: Lang }) {
  if (review === 'none') return null
  return <span className={`review ${review}`}>{t(lang, `review.${review}`)}</span>
}

export function Insight({ line, lang }: { line: Line; lang: Lang }) {
  return (
    <p className="insight">
      <b>{t(lang, 'whyItMatters')}</b> <Text line={line} lang={lang} />
    </p>
  )
}

/** Translation steps are tagged with their language, e.g. "Translator (fr)"; viewers see only their own language's. */
const LANGUAGE_STEP = /\((\w{2})\)$/

/** How this overlay was made: each agent and code check, in order. */
export function Trace({ steps, lang }: { steps: AgentStep[]; lang: Lang }) {
  const shown = steps.filter((s) => (s.agent.match(LANGUAGE_STEP)?.[1] ?? lang) === lang)
  return (
    <ol className="trace" aria-label={t(lang, 'howMade')}>
      {shown.map((s, i) => (
        <li key={i} className={s.model === 'code' ? 'code' : 'agent'}>
          <span className="who">
            {s.agent}
            <small>{s.model === 'code' ? t(lang, 'code') : s.model}</small>
          </span>
          <span className="what">
            {s.outcome}
            {s.durationMs > 0 && <small> · {num(lang, s.durationMs / 1000, 1)} s</small>}
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
