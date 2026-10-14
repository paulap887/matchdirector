import { METRICS } from '../metrics'
import { clock } from '../types'
import type { View } from '../view'

export function Scorebug({ view }: { view: View }) {
  const { info, snapshot, club } = view
  const side = (teamId: string) => (club ? (club.teamId === teamId ? 'mine' : 'theirs') : '')
  const metric = view.metric && snapshot ? METRICS[view.metric] : undefined

  return (
    <div className="scorebug">
      <span className={`team home ${side(info.home.teamId)}`}>{info.home.name}</span>
      <span className="score">
        {snapshot?.home.goals ?? 0}
        <i>–</i>
        {snapshot?.away.goals ?? 0}
      </span>
      <span className={`team away ${side(info.away.teamId)}`}>{info.away.name}</span>
      <span className="clock">
        {snapshot ? clock(snapshot.matchClock) : '00:00'}
        <small>{snapshot?.period === 2 ? '2nd half' : '1st half'}</small>
      </span>
      <span className="venue">
        {info.venue}
        {metric && (
          <b className="pinned" title="Your pinned stat">
            📌 {metric.label}: {metric.single ? metric.format(metric.home(view)) : `${metric.format(metric.home(view))} – ${metric.format(metric.away(view))}`}
          </b>
        )}
      </span>
    </div>
  )
}
