import { useState } from 'react'
import type { MatchRecap } from '../types'
import { variantFor, type View } from '../view'
import { Trace } from './OverlayCard'

/** Full-time report for this viewer's mode, with the player of the match and how the report was made. */
export function RecapCard({ recap, view }: { recap: MatchRecap; view: View }) {
  const [why, setWhy] = useState(false)
  const players = [...view.info.home.players, ...view.info.away.players]
  const best = players.find((p) => p.playerId === recap.playerOfTheMatch)
  const bestTeam = best && (view.info.home.players.includes(best) ? view.info.home : view.info.away)
  const text = recap.variants[variantFor(view)] ?? recap.variants.casual
  const m = recap.metrics

  return (
    <article className="card recap" aria-live="polite">
      <div className="card-head">
        <span className="tag">Full-time report</span>
        {recap.variants.source === 'agent' && <span className="review approved">✓ fact-checked</span>}
      </div>
      <h2 className="headline">{recap.headline}</h2>
      <p className="line">{text}</p>
      {best && (
        <p className="potm">
          ⭐ Player of the match: <b>#{best.number} {best.name}</b> ({bestTeam?.name}) · rating {m.playerOfTheMatchRating}
        </p>
      )}
      <button className="why" onClick={() => setWhy(!why)} aria-expanded={why}>
        {why ? 'Hide evidence' : 'Why am I seeing this?'}
      </button>
      {why && (
        <div className="evidence">
          {recap.trace && <Trace steps={recap.trace} />}
          <p className="cites">
            Built from {recap.evidence.length} cited events across the match's key moments. Player of the match is chosen by a published
            formula in the stats engine, not by the AI.
          </p>
        </div>
      )}
    </article>
  )
}
