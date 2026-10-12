import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { useEffect, useState } from 'react'
import type { MatchInfo, MatchSnapshot, Overlay } from './types'

type Feed = { info?: MatchInfo; snapshot?: MatchSnapshot; overlays: Overlay[]; connected: boolean }

/** Live match state from the overlay hub: initial state over HTTP, then pushes over SignalR. */
export function useMatchFeed(): Feed {
  const [feed, setFeed] = useState<Feed>({ overlays: [], connected: false })

  useEffect(() => {
    let disposed = false
    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/match')
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()

    connection.on('matchInfo', (info: MatchInfo) => setFeed((f) => ({ ...f, info, snapshot: undefined, overlays: [] })))
    connection.on('snapshot', (snapshot: MatchSnapshot) => setFeed((f) => ({ ...f, snapshot })))
    connection.on('overlay', (overlay: Overlay) => setFeed((f) => ({ ...f, overlays: [overlay, ...f.overlays].slice(0, 30) })))
    connection.onreconnected(() => setFeed((f) => ({ ...f, connected: true })))
    connection.onreconnecting(() => setFeed((f) => ({ ...f, connected: false })))

    fetch('/api/state')
      .then((r) => r.json())
      .then((s: { matchInfo?: MatchInfo; snapshot?: MatchSnapshot; overlays: Overlay[] }) => {
        if (!disposed)
          setFeed((f) => ({ ...f, info: s.matchInfo ?? undefined, snapshot: s.snapshot ?? undefined, overlays: [...s.overlays].reverse() }))
      })
      .catch(() => {})

    connection
      .start()
      .then(() => !disposed && setFeed((f) => ({ ...f, connected: true })))
      .catch(() => {})

    return () => {
      disposed = true
      void connection.stop()
    }
  }, [])

  return feed
}
