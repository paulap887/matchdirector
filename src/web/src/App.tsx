import { useEffect, useMemo, useState } from 'react'
import './App.css'
import { clock, type MatchInfo, type MatchSnapshot, type Mode, type Overlay } from './types'
import { useMatchFeed } from './useMatchFeed'

const LABELS: Record<string, string> = {
  Goal: 'Goal',
  BigChanceMissed: 'Big chance',
  KeySave: 'Key save',
  PassingMove: 'Passing move',
  DifficultPass: 'Killer pass',
  HighRegain: 'High regain',
  MomentumShift: 'Momentum',
  TempoShift: 'Tempo',
  HalfTime: 'Half-time',
  FullTime: 'Full-time',
}

export default function App() {
  const { info, snapshot, overlays, connected } = useMatchFeed()
  const [mode, setMode] = useState<Mode>('casual')
  const live = useLiveOverlay(overlays)

  return (
    <div className="studio">
      <header className="topbar">
        <div className="brand">
          Match<span>Director</span>
        </div>
        <div className="mode" role="group" aria-label="Viewer mode">
          {(['casual', 'analyst'] as Mode[]).map((m) => (
            <button key={m} className={m === mode ? 'active' : ''} onClick={() => setMode(m)}>
              {m === 'casual' ? 'Casual fan' : 'Analyst'}
            </button>
          ))}
        </div>
        <div className={`status ${connected ? 'on' : ''}`}>{connected ? 'Live' : 'Connecting…'}</div>
      </header>

      {info ? <Scorebug info={info} snapshot={snapshot} /> : <div className="waiting">Waiting for kick-off…</div>}

      <main className="grid">
        <section className="stage">
          {live ? <OverlayCard key={live.overlayId} overlay={live} mode={mode} info={info} /> : <div className="idle">Watching the game…</div>}
          {snapshot && info && <Meters info={info} snapshot={snapshot} />}
        </section>

        <aside className="feed">
          <h2>Moments</h2>
          <ol>
            {overlays.map((o) => (
              <li key={o.overlayId} className={`t-${o.type}`}>
                <span className="time">{clock(o.matchClock)}</span>
                <span className="tag">{LABELS[o.type] ?? o.type}</span>
                <p>{o.variants[mode]}</p>
              </li>
            ))}
          </ol>
        </aside>
      </main>

      <footer>All clubs, players and match data are fictional. Numbers are computed by code; AI only words them.</footer>
    </div>
  )
}

/** The newest overlay stays on screen for its duration (capped for fast replays). */
function useLiveOverlay(overlays: Overlay[]): Overlay | undefined {
  const latest = overlays[0]
  const [expired, setExpired] = useState<string>()
  useEffect(() => {
    if (!latest) return
    const timer = setTimeout(() => setExpired(latest.overlayId), Math.min(latest.durationMs, 8000))
    return () => clearTimeout(timer)
  }, [latest])
  return latest && latest.overlayId !== expired ? latest : undefined
}

function Scorebug({ info, snapshot }: { info: MatchInfo; snapshot?: MatchSnapshot }) {
  return (
    <div className="scorebug">
      <span className="team home">{info.home.name}</span>
      <span className="score">
        {snapshot?.home.goals ?? 0}
        <i>–</i>
        {snapshot?.away.goals ?? 0}
      </span>
      <span className="team away">{info.away.name}</span>
      <span className="clock">
        {snapshot ? clock(snapshot.matchClock) : '00:00'}
        <small>{snapshot?.period === 2 ? '2nd half' : '1st half'}</small>
      </span>
      <span className="venue">{info.venue}</span>
    </div>
  )
}

function OverlayCard({ overlay, mode, info }: { overlay: Overlay; mode: Mode; info?: MatchInfo }) {
  const [why, setWhy] = useState(false)
  const player = useMemo(
    () => [...(info?.home.players ?? []), ...(info?.away.players ?? [])].find((p) => p.playerId === overlay.playerId),
    [info, overlay.playerId],
  )
  const source = overlay.variants.source === 'agent' ? 'AI narrated · numbers verified by code' : 'Template narration'

  return (
    <article className={`card t-${overlay.type}`} aria-live="polite">
      <div className="card-head">
        <span className="tag">{LABELS[overlay.type] ?? overlay.type}</span>
        <span className="time">{clock(overlay.matchClock)}</span>
        {player && (
          <span className="player">
            #{player.number} {player.name}
          </span>
        )}
      </div>
      <p className="line">{overlay.variants[mode]}</p>
      <button className="why" onClick={() => setWhy(!why)} aria-expanded={why}>
        {why ? 'Hide' : 'Why am I seeing this?'}
      </button>
      {why && (
        <div className="evidence">
          <p className="source">{source}</p>
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
            Evidence: {overlay.evidence.map((id) => <code key={id}>{id.split('-').pop()}</code>)}
          </p>
        </div>
      )}
    </article>
  )
}

function Meters({ info, snapshot }: { info: MatchInfo; snapshot: MatchSnapshot }) {
  const { home, away, momentum, chaos } = snapshot
  const rows: [string, number, number, (v: number) => string][] = [
    ['Possession', home.possession, away.possession, (v) => `${Math.round(v)}%`],
    ['xG', home.xg, away.xg, (v) => v.toFixed(2)],
    ['Shots (on target)', home.shots, away.shots, (v) => String(v)],
    ['Pass accuracy', home.passAccuracy, away.passAccuracy, (v) => `${Math.round(v)}%`],
    ['Progressive passes', home.progressivePasses, away.progressivePasses, (v) => String(v)],
  ]

  return (
    <div className="meters">
      <div className="stats">
        {rows.map(([label, h, a, fmt]) => (
          <div className="stat" key={label}>
            <span className="h">
              {fmt(h)}
              {label.startsWith('Shots') && <small> ({home.shotsOnTarget})</small>}
            </span>
            <span className="label">{label}</span>
            <span className="a">
              {fmt(a)}
              {label.startsWith('Shots') && <small> ({away.shotsOnTarget})</small>}
            </span>
            <div className="bar">
              <div style={{ width: `${h + a === 0 ? 50 : (100 * h) / (h + a)}%` }} />
            </div>
          </div>
        ))}
      </div>

      <div className="gauge">
        <div className="gauge-label">
          <span>{info.away.name}</span>
          <strong>Momentum</strong>
          <span>{info.home.name}</span>
        </div>
        <div className="track momentum">
          <div className="needle" style={{ left: `${50 + momentum / 2}%` }} />
        </div>
      </div>

      <div className="gauge">
        <div className="gauge-label">
          <span>Control</span>
          <strong>Game state</strong>
          <span>Chaos</span>
        </div>
        <div className="track chaos">
          <div className="needle" style={{ left: `${chaos}%` }} />
        </div>
      </div>
    </div>
  )
}
