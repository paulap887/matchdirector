import { useState } from 'react'
import { num, t } from '../i18n'
import type { MatchRecap } from '../types'
import { lineFor, type View } from '../view'
import { Text, Trace } from './OverlayCard'

/** Full-time report for this viewer's mode and language, with the player of the match and how the report was made. */
export function RecapCard({ recap, view }: { recap: MatchRecap; view: View }) {
  const { lang } = view
  const [why, setWhy] = useState(false)
  const players = [...view.info.home.players, ...view.info.away.players]
  const best = players.find((p) => p.playerId === recap.playerOfTheMatch)
  const bestTeam = best && (view.info.home.players.includes(best) ? view.info.home : view.info.away)
  const headline = (lang !== 'en' && recap.variants[`${lang}:headline`]) || recap.headline
  const m = recap.metrics

  return (
    <article className="card recap" aria-live="polite">
      <div className="card-head">
        <span className="tag">{t(lang, 'recap.tag')}</span>
        {recap.variants.source === 'agent' && <span className="review approved">{t(lang, 'review.approved')}</span>}
      </div>
      <h2 className="headline">{headline}</h2>
      <p className="line">
        <Text line={lineFor(recap.variants, view)} lang={lang} />
      </p>
      {best && (
        <p className="potm">
          ⭐ {t(lang, 'recap.potm')}:{' '}
          <b>
            #{best.number} {best.name}
          </b>{' '}
          ({bestTeam?.name}) · {t(lang, 'recap.rating')} {num(lang, m.playerOfTheMatchRating, 1)}
        </p>
      )}
      <button className="why" onClick={() => setWhy(!why)} aria-expanded={why}>
        {t(lang, why ? 'hideEvidence' : 'why')}
      </button>
      {why && (
        <div className="evidence">
          {recap.trace && <Trace steps={recap.trace} lang={lang} />}
          <p className="cites">{t(lang, 'recap.built', { n: recap.evidence.length })}</p>
        </div>
      )}
    </article>
  )
}
