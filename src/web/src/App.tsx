import { useEffect, useState } from 'react'
import './App.css'
import { Feed } from './components/Feed'
import { Meters } from './components/Meters'
import { OverlayCard } from './components/OverlayCard'
import { PlayerCard, TopPlayers } from './components/Players'
import { RecapCard } from './components/RecapCard'
import { Scorebug } from './components/Scorebug'
import { METRIC_OPTIONS } from './metrics'
import { usePrefs } from './prefs'
import type { MatchInfo, MetricKey, Mode, Overlay } from './types'
import { useMatchFeed } from './useMatchFeed'
import { buildView, mood, visible, type View } from './view'

const MODES: { key: Mode; label: string }[] = [
  { key: 'casual', label: 'Casual fan' },
  { key: 'analyst', label: 'Analyst' },
  { key: 'club', label: 'Club fan' },
  { key: 'player', label: 'Player focus' },
]

export default function App() {
  const { info, snapshot, overlays, connected, status, recap } = useMatchFeed()
  const [prefs, setPrefs] = usePrefs()
  const view = info ? buildView(prefs, info, snapshot) : undefined
  const live = useLiveOverlay(view ? overlays.filter((o) => visible(o, view)) : [])

  return (
    <div className={`studio mode-${prefs.mode}`}>
      <header className="topbar">
        <div className="brand">
          Match<span>Director</span>
        </div>
        <nav className="mode" aria-label="Viewer mode">
          {MODES.map((m) => (
            <button key={m.key} className={m.key === prefs.mode ? 'active' : ''} onClick={() => setPrefs({ mode: m.key })}>
              {m.label}
            </button>
          ))}
        </nav>
        <div className={`status ${connected ? 'on' : ''}`}>{connected ? 'Live' : 'Connecting…'}</div>
      </header>

      {info && <Controls info={info} prefs={prefs} setPrefs={setPrefs} />}

      {connected && status === 'idle' && <KickOff hasPlayed={!!info} />}

      {view ? <Scorebug view={view} /> : <div className="waiting">Waiting for kick-off…</div>}

      {view && (
        <main className="grid">
          <section className="stage">
            {recap && recap.matchId === view.info.matchId && <RecapCard recap={recap} view={view} />}
            {prefs.mode === 'player' && <PlayerCard view={view} />}
            {mood(view) && <p className="mood">{mood(view)}</p>}
            {live ? <OverlayCard key={`${live.overlayId}-${prefs.mode}`} overlay={live} view={view} /> : <Idle view={view} />}
            <Meters view={view} />
            {prefs.mode === 'analyst' && <TopPlayers view={view} />}
          </section>
          <Feed overlays={overlays} view={view} />
        </main>
      )}

      <footer>All clubs, players and match data are fictional. Numbers are computed by code; AI only words them.</footer>
    </div>
  )
}

function Controls({ info, prefs, setPrefs }: { info: MatchInfo; prefs: ReturnType<typeof usePrefs>[0]; setPrefs: ReturnType<typeof usePrefs>[1] }) {
  return (
    <div className="controls">
      {prefs.mode === 'club' && (
        <div className="clubs" role="group" aria-label="Your club">
          <span>Your club</span>
          {[info.home, info.away].map((t, i) => (
            <button key={t.teamId} className={`${i === 0 ? 'home' : 'away'} ${prefs.clubId === t.teamId ? 'active' : ''}`} onClick={() => setPrefs({ clubId: t.teamId })}>
              {t.name}
            </button>
          ))}
        </div>
      )}
      {prefs.mode === 'player' && (
        <label>
          Follow
          <select value={prefs.playerId ?? ''} onChange={(e) => setPrefs({ playerId: e.target.value || undefined })}>
            <option value="">Choose a player…</option>
            {[info.home, info.away].map((t) => (
              <optgroup key={t.teamId} label={t.name}>
                {t.players.map((p) => (
                  <option key={p.playerId} value={p.playerId}>
                    #{p.number} {p.name} ({p.position})
                  </option>
                ))}
              </optgroup>
            ))}
          </select>
        </label>
      )}
      <label>
        Pin a stat
        <select value={prefs.metric ?? ''} onChange={(e) => setPrefs({ metric: (e.target.value || undefined) as MetricKey | undefined })}>
          <option value="">None</option>
          {METRIC_OPTIONS.map((m) => (
            <option key={m.key} value={m.key}>
              {m.label}
            </option>
          ))}
        </select>
      </label>
    </div>
  )
}

/**
 * Matches play on demand, so the studio's models only run while someone is watching. Connecting already asks for
 * a match, so a new viewer sees it start within seconds; afterwards the button asks for another.
 */
function KickOff({ hasPlayed }: { hasPlayed: boolean }) {
  const [requested, setRequested] = useState(true)
  useEffect(() => {
    if (!requested) return
    const timer = setTimeout(() => setRequested(false), 20000)
    return () => clearTimeout(timer)
  }, [requested])

  const kickOff = () => {
    setRequested(true)
    void fetch('/api/kickoff', { method: 'POST' })
  }

  return (
    <div className="kickoff" role="status">
      <div>
        <strong>{requested ? 'Kicking off…' : hasPlayed ? 'Full time. Fancy another?' : 'No match on right now.'}</strong>
        <p>Matches play on demand, so the AI studio only runs while someone is watching.</p>
      </div>
      <button onClick={kickOff} disabled={requested}>
        {requested ? 'Starting…' : 'Kick off a match'}
      </button>
    </div>
  )
}

function Idle({ view }: { view: View }) {
  const text =
    view.mode === 'club' && !view.club
      ? 'Pick your club above.'
      : view.mode === 'player' && view.player
        ? `Waiting for ${view.player.name} to get involved…`
        : 'Watching the game…'
  return <div className="idle">{text}</div>
}

/** The newest overlay for this viewer stays on screen for its duration (capped for fast replays). */
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
