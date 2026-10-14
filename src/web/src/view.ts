import type { MatchInfo, MatchSnapshot, MetricKey, Mode, Overlay, PlayerInfo, TeamInfo } from './types'
import type { Prefs } from './prefs'

/** Everything a component needs to render the current viewer's personalised view. */
export type View = {
  mode: Mode
  info: MatchInfo
  snapshot?: MatchSnapshot
  club?: TeamInfo
  player?: PlayerInfo & { team: TeamInfo }
  metric?: MetricKey
}

export function buildView(prefs: Prefs, info: MatchInfo, snapshot?: MatchSnapshot): View {
  const club = [info.home, info.away].find((t) => t.teamId === prefs.clubId)
  const team = [info.home, info.away].find((t) => t.players.some((p) => p.playerId === prefs.playerId))
  const player = team?.players.find((p) => p.playerId === prefs.playerId)
  return { mode: prefs.mode, info, snapshot, club, player: player && team && { ...player, team }, metric: prefs.metric }
}

/** The line this viewer should read for an overlay. */
export function lineFor(overlay: Overlay, view: View): string {
  if (view.mode === 'analyst') return overlay.variants.analyst
  if (view.mode === 'club' && view.club)
    return overlay.variants[view.club.teamId === view.info.home.teamId ? 'homeFan' : 'awayFan'] ?? overlay.variants.casual
  return overlay.variants.casual
}

/** Which overlays this viewer sees: casual fans get the headline moments, player focus only that player's. */
export function visible(overlay: Overlay, view: View): boolean {
  if (view.mode === 'casual') return overlay.priority >= 45
  if (view.mode === 'player') return !!view.player && (overlay.participants ?? [overlay.playerId]).includes(view.player.playerId)
  return true
}

export function teamOf(overlay: Overlay, info: MatchInfo): TeamInfo | undefined {
  return [info.home, info.away].find((t) => t.players.some((p) => p.playerId === overlay.playerId))
}

/** Momentum and tempo in plain words, from the viewer's side where there is one. */
export function mood(view: View): string | undefined {
  const s = view.snapshot
  if (!s) return undefined
  const leader = s.momentum >= 25 ? view.info.home : s.momentum <= -25 ? view.info.away : undefined
  const tempo = s.chaos >= 60 ? 'end-to-end' : s.chaos <= 40 ? 'calm and controlled' : 'evenly poised'
  if (!leader) return `Nobody on top · ${tempo}`
  if (view.mode === 'club' && view.club) return `${leader === view.club ? "We're" : "They're"} on top · ${tempo}`
  return `${leader.name} on top · ${tempo}`
}
