import type { PlayerStats } from '../types'
import type { View } from '../view'

/** Focus card for one player: their live numbers, computed by the stats engine. */
export function PlayerCard({ view }: { view: View }) {
  const { player, snapshot } = view
  if (!player) return <div className="idle">Pick a player to follow.</div>
  const s = snapshot?.players.find((p) => p.playerId === player.playerId)

  const stats: [string, string][] = s
    ? [
        ['Passes', `${s.passesCompleted}/${s.passes}`],
        ['Progressive passes', String(s.progressivePasses)],
        ['Shots', String(s.shots)],
        ['Goals', String(s.goals)],
        ['xG', s.xg.toFixed(2)],
        ['Carried', `${Math.round(s.distanceCarried)} m`],
        ['Tackles won', String(s.tackles)],
        ['Interceptions', String(s.interceptions)],
        ['Fastest shot', s.fastestShot ? `${s.fastestShot} m/s` : '–'],
      ]
    : []

  return (
    <div className={`player-card ${player.team.teamId === view.info.home.teamId ? 'home' : 'away'}`}>
      <div className="player-head">
        <span className="number">{player.number}</span>
        <div>
          <strong>{player.name}</strong>
          <small>
            {player.position} · {player.team.name}
          </small>
        </div>
      </div>
      {s ? (
        <dl>
          {stats.map(([k, v]) => (
            <div key={k}>
              <dt>{k}</dt>
              <dd>{v}</dd>
            </div>
          ))}
        </dl>
      ) : (
        <p className="empty">No touches yet.</p>
      )}
    </div>
  )
}

/** Analyst view: the most involved players so far. */
export function TopPlayers({ view }: { view: View }) {
  const players = [...(view.snapshot?.players ?? [])]
    .sort((a, b) => score(b) - score(a))
    .slice(0, 6)
  const name = (p: PlayerStats) => [...view.info.home.players, ...view.info.away.players].find((x) => x.playerId === p.playerId)
  if (players.length === 0) return null

  return (
    <div className="top-players">
      <h3>Most influential</h3>
      <table>
        <thead>
          <tr>
            <th>Player</th>
            <th>xG</th>
            <th>Prog.</th>
            <th>Pass %</th>
            <th>Def.</th>
          </tr>
        </thead>
        <tbody>
          {players.map((p) => (
            <tr key={p.playerId} className={p.teamId === view.info.home.teamId ? 'home' : 'away'}>
              <td>
                #{name(p)?.number} {name(p)?.name}
              </td>
              <td>{p.xg.toFixed(2)}</td>
              <td>{p.progressivePasses}</td>
              <td>{p.passes ? Math.round((100 * p.passesCompleted) / p.passes) : 0}</td>
              <td>{p.tackles + p.interceptions}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

const score = (p: PlayerStats) => p.xg * 10 + p.progressivePasses + p.goals * 5 + p.tackles + p.interceptions
