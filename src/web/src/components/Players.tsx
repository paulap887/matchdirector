import { num, pct, t, type Key } from '../i18n'
import type { PlayerStats } from '../types'
import type { View } from '../view'

/** Focus card for one player: their live numbers, computed by the stats engine. */
export function PlayerCard({ view }: { view: View }) {
  const { player, snapshot, lang } = view
  if (!player) return <div className="idle">{t(lang, 'pickPlayer')}</div>
  const s = snapshot?.players.find((p) => p.playerId === player.playerId)
  const speed = (v?: number) => (v ? `${num(lang, v, 1)} m/s` : '–')

  const stats: [Key, string][] = s
    ? [
        ['stat.passes', `${s.passesCompleted}/${s.passes}`],
        ['stat.progressive', num(lang, s.progressivePasses)],
        ['stat.shots', num(lang, s.shots)],
        ['stat.goals', num(lang, s.goals)],
        ['stat.xg', num(lang, s.xg, 2)],
        ['stat.carried', `${num(lang, s.distanceCarried)} m`],
        ['stat.tackles', num(lang, s.tackles)],
        ['stat.interceptions', num(lang, s.interceptions)],
        ['stat.fastestShot', speed(s.fastestShot)],
        ['stat.topCarry', speed(s.topCarrySpeed)],
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
              <dt>{t(lang, k)}</dt>
              <dd>{v}</dd>
            </div>
          ))}
        </dl>
      ) : (
        <p className="empty">{t(lang, 'noTouches')}</p>
      )}
    </div>
  )
}

/** Analyst view: the most involved players so far. */
export function TopPlayers({ view }: { view: View }) {
  const { lang } = view
  const players = [...(view.snapshot?.players ?? [])]
    .sort((a, b) => score(b) - score(a))
    .slice(0, 6)
  const name = (p: PlayerStats) => [...view.info.home.players, ...view.info.away.players].find((x) => x.playerId === p.playerId)
  if (players.length === 0) return null

  return (
    <div className="top-players">
      <h3>{t(lang, 'mostInfluential')}</h3>
      <table>
        <thead>
          <tr>
            <th>{t(lang, 'col.player')}</th>
            <th>xG</th>
            <th>{t(lang, 'col.prog')}</th>
            <th>{t(lang, 'col.passPct')}</th>
            <th>{t(lang, 'col.def')}</th>
          </tr>
        </thead>
        <tbody>
          {players.map((p) => (
            <tr key={p.playerId} className={p.teamId === view.info.home.teamId ? 'home' : 'away'}>
              <td>
                #{name(p)?.number} {name(p)?.name}
              </td>
              <td>{num(lang, p.xg, 2)}</td>
              <td>{p.progressivePasses}</td>
              <td>{pct(lang, p.passes ? (100 * p.passesCompleted) / p.passes : 0)}</td>
              <td>{p.tackles + p.interceptions}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

const score = (p: PlayerStats) => p.xg * 10 + p.progressivePasses + p.goals * 5 + p.tackles + p.interceptions
