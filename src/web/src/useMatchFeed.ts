import { HubConnectionBuilder, HubConnectionState, LogLevel, type HubConnection } from '@microsoft/signalr'
import { useEffect, useRef, useState } from 'react'
import type { Lang } from './i18n'
import type { MatchInfo, MatchRecap, MatchSnapshot, Overlay } from './types'

export type Status = 'idle' | 'playing'

type Feed = { info?: MatchInfo; snapshot?: MatchSnapshot; overlays: Overlay[]; connected: boolean; status: Status; recap?: MatchRecap }

/**
 * Live match state from the overlay hub: initial state over HTTP, then pushes over SignalR. The hub is told the viewer's
 * language, so the studio translates into it (and only into languages someone is watching in).
 */
export function useMatchFeed(lang: Lang): Feed {
  const [feed, setFeed] = useState<Feed>({ overlays: [], connected: false, status: 'idle' })
  const hub = useRef<HubConnection>(undefined)
  const language = useRef(lang)

  useEffect(() => {
    language.current = lang
    if (hub.current?.state === HubConnectionState.Connected) void hub.current.invoke('SetLanguage', lang).catch(() => {})
  }, [lang])

  useEffect(() => {
    let disposed = false
    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/match')
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()
    hub.current = connection
    const tellLanguage = () => void connection.invoke('SetLanguage', language.current).catch(() => {})

    // Match info is re-sent periodically; only a different match resets the screen.
    connection.on('matchInfo', (info: MatchInfo) =>
      setFeed((f) => (f.info?.matchId === info.matchId ? { ...f, info } : { ...f, info, snapshot: undefined, overlays: [], recap: undefined })),
    )
    connection.on('snapshot', (snapshot: MatchSnapshot) => setFeed((f) => ({ ...f, snapshot })))
    // Reviewed overlays come back with the same ID: replace in place, otherwise add to the top.
    connection.on('overlay', (overlay: Overlay) =>
      setFeed((f) => ({
        ...f,
        overlays: f.overlays.some((o) => o.overlayId === overlay.overlayId)
          ? f.overlays.map((o) => (o.overlayId === overlay.overlayId ? overlay : o))
          : [overlay, ...f.overlays].slice(0, 30),
      })),
    )
    connection.on('recap', (recap: MatchRecap) => setFeed((f) => ({ ...f, recap })))
    connection.on('status', (s: { status: Status }) => setFeed((f) => ({ ...f, status: s.status })))
    connection.onreconnected(() => {
      tellLanguage()
      setFeed((f) => ({ ...f, connected: true }))
    })
    connection.onreconnecting(() => setFeed((f) => ({ ...f, connected: false })))

    fetch('/api/state')
      .then((r) => r.json())
      .then((s: { matchInfo?: MatchInfo; snapshot?: MatchSnapshot; overlays: Overlay[]; status?: Status; recap?: MatchRecap }) => {
        if (!disposed)
          setFeed((f) => ({
            ...f,
            info: s.matchInfo ?? undefined,
            snapshot: s.snapshot ?? undefined,
            overlays: [...s.overlays].reverse(),
            status: s.status ?? 'idle',
            recap: s.recap ?? undefined,
          }))
      })
      .catch(() => {})

    connection
      .start()
      .then(() => {
        if (disposed) return
        tellLanguage()
        setFeed((f) => ({ ...f, connected: true }))
      })
      .catch(() => {})

    return () => {
      disposed = true
      void connection.stop()
    }
  }, [])

  return feed
}
