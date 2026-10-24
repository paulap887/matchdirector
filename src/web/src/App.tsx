import { useEffect, useState } from 'react'
import './App.css'
import { Feed } from './components/Feed'
import { Meters } from './components/Meters'
import { OverlayCard } from './components/OverlayCard'
import { PlayerCard, TopPlayers } from './components/Players'
import { RecapCard } from './components/RecapCard'
import { Scorebug } from './components/Scorebug'
import { LANGS, t, type Lang } from './i18n'
import { METRIC_OPTIONS } from './metrics'
import { usePrefs } from './prefs'
import type { MatchInfo, MetricKey, Mode, Overlay } from './types'
import { useMatchFeed } from './useMatchFeed'
import { buildView, mood, visible, type View } from './view'

const MODES: Mode[] = ['casual', 'analyst', 'club', 'player']

export default function App() {
  const [prefs, setPrefs] = usePrefs()
  const { lang } = prefs
  const { info, snapshot, overlays, connected, status, recap } = useMatchFeed(lang)
  useEffect(() => {
    document.documentElement.lang = lang
  }, [lang])
  const view = info ? buildView(prefs, info, snapshot) : undefined
  const live = useLiveOverlay(view ? overlays.filter((o) => visible(o, view)) : [])

  return (
    <div className={`studio mode-${prefs.mode}`}>
      <header className="topbar">
        <div className="brand">
          Ninety<span>fold</span>
        </div>
        <nav className="mode" aria-label={t(lang, 'viewerMode')}>
          {MODES.map((m) => (
            <button key={m} className={m === prefs.mode ? 'active' : ''} onClick={() => setPrefs({ mode: m })}>
              {t(lang, `mode.${m}`)}
            </button>
          ))}
        </nav>
        <select className="lang" aria-label={t(lang, 'language')} value={lang} onChange={(e) => setPrefs({ lang: e.target.value as Lang })}>
          {LANGS.map((l) => (
            <option key={l.code} value={l.code}>
              {l.name}
            </option>
          ))}
        </select>
        <div className={`status ${connected ? 'on' : ''}`}>{t(lang, connected ? 'live' : 'connecting')}</div>
      </header>

      {info && <Controls info={info} prefs={prefs} setPrefs={setPrefs} />}

      {connected && status === 'idle' && <KickOff hasPlayed={!!info} lang={lang} />}

      {view ? <Scorebug view={view} /> : <div className="waiting">{t(lang, 'waiting')}</div>}

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

      <footer>{t(lang, 'footer')}</footer>
    </div>
  )
}

function Controls({ info, prefs, setPrefs }: { info: MatchInfo; prefs: ReturnType<typeof usePrefs>[0]; setPrefs: ReturnType<typeof usePrefs>[1] }) {
  const { lang } = prefs
  return (
    <div className="controls">
      {prefs.mode === 'club' && (
        <div className="clubs" role="group" aria-label={t(lang, 'yourClub')}>
          <span>{t(lang, 'yourClub')}</span>
          {[info.home, info.away].map((t, i) => (
            <button key={t.teamId} className={`${i === 0 ? 'home' : 'away'} ${prefs.clubId === t.teamId ? 'active' : ''}`} onClick={() => setPrefs({ clubId: t.teamId })}>
              {t.name}
            </button>
          ))}
        </div>
      )}
      {prefs.mode === 'player' && (
        <label>
          {t(lang, 'follow')}
          <select value={prefs.playerId ?? ''} onChange={(e) => setPrefs({ playerId: e.target.value || undefined })}>
            <option value="">{t(lang, 'choosePlayer')}</option>
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
        {t(lang, 'pinStat')}
        <select value={prefs.metric ?? ''} onChange={(e) => setPrefs({ metric: (e.target.value || undefined) as MetricKey | undefined })}>
          <option value="">{t(lang, 'none')}</option>
          {METRIC_OPTIONS.map((m) => (
            <option key={m.key} value={m.key}>
              {t(lang, m.label)}
            </option>
          ))}
        </select>
      </label>
    </div>
  )
}

/**
 * Matches play on demand, so the studio's models only run while someone is watching. A new visitor's connection
 * already asks for a match, so they see it start within seconds; after a match, the button asks for another.
 */
function KickOff({ hasPlayed, lang }: { hasPlayed: boolean; lang: Lang }) {
  const [requested, setRequested] = useState(!hasPlayed)
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
        <strong>{t(lang, requested ? 'kickingOff' : hasPlayed ? 'fullTimeAnother' : 'noMatch')}</strong>
        <p>{t(lang, 'onDemand')}</p>
      </div>
      <button onClick={kickOff} disabled={requested}>
        {t(lang, requested ? 'starting' : 'kickOff')}
      </button>
    </div>
  )
}

function Idle({ view }: { view: View }) {
  const text =
    view.mode === 'club' && !view.club
      ? t(view.lang, 'pickClub')
      : view.mode === 'player' && view.player
        ? t(view.lang, 'waitingFor', { name: view.player.name })
        : t(view.lang, 'watching')
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
