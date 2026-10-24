import { t, translated, type Lang } from './i18n'
import type { MatchInfo, MatchSnapshot, MetricKey, Mode, Overlay, PlayerInfo, TeamInfo } from './types'
import type { Prefs } from './prefs'

/** Everything a component needs to render the current viewer's personalised view. */
export type View = {
  mode: Mode
  lang: Lang
  info: MatchInfo
  snapshot?: MatchSnapshot
  club?: TeamInfo
  player?: PlayerInfo & { team: TeamInfo }
  metric?: MetricKey
}

/** A line the studio wrote, and whether it is shown in English because no checked translation exists (yet). */
export type Line = { text: string; english: boolean }

export function buildView(prefs: Prefs, info: MatchInfo, snapshot?: MatchSnapshot): View {
  const club = [info.home, info.away].find((t) => t.teamId === prefs.clubId)
  const team = [info.home, info.away].find((t) => t.players.some((p) => p.playerId === prefs.playerId))
  const player = team?.players.find((p) => p.playerId === prefs.playerId)
  return { mode: prefs.mode, lang: prefs.lang, info, snapshot, club, player: player && team && { ...player, team }, metric: prefs.metric }
}

/** The variant key this viewer reads: analyst, a club's fans, or the casual line. */
export function variantFor(view: View): string {
  if (view.mode === 'analyst') return 'analyst'
  if (view.mode === 'club' && view.club) return view.club.teamId === view.info.home.teamId ? 'homeFan' : 'awayFan'
  return 'casual'
}

/** The line this viewer should read for an overlay or report, in their language where a translation passed its check. */
export function lineFor(variants: Record<string, string>, view: View): Line {
  return translated(variants, variantFor(view), view.lang) ?? translated(variants, 'casual', view.lang) ?? { text: '', english: false }
}

/** Which overlays this viewer sees: casual fans get the headline moments, player focus only that player's. */
export function visible(overlay: Overlay, view: View): boolean {
  if (view.mode === 'casual') return overlay.priority >= 45
  if (view.mode === 'player') return !!view.player && (overlay.participants ?? [overlay.playerId]).includes(view.player.playerId)
  return true
}

/** The club a moment belongs to, from its main player or else the players involved; undefined for match-wide moments. */
const MATCH_WIDE = ['TempoShift', 'HalfTime', 'FullTime']

export function teamOf(overlay: Overlay, info: MatchInfo): TeamInfo | undefined {
  if (MATCH_WIDE.includes(overlay.type)) return undefined
  const playerId = overlay.playerId ?? overlay.participants?.[0]
  return playerId ? [info.home, info.away].find((t) => t.players.some((p) => p.playerId === playerId)) : undefined
}

/** For club fans: true for their club's moments, false for the other club's, undefined for match-wide ones. */
export function isOurs(overlay: Overlay, view: View): boolean | undefined {
  if (view.mode !== 'club' || !view.club) return undefined
  const team = teamOf(overlay, view.info)
  return team ? team.teamId === view.club.teamId : undefined
}

/** Momentum and tempo in plain words, from the viewer's side where there is one. */
export function mood(view: View): string | undefined {
  const s = view.snapshot
  if (!s) return undefined
  const { lang } = view
  const leader = s.momentum >= 25 ? view.info.home : s.momentum <= -25 ? view.info.away : undefined
  const tempo = t(lang, s.chaos >= 60 ? 'tempo.chaotic' : s.chaos <= 40 ? 'tempo.calm' : 'tempo.even')
  if (!leader) return `${t(lang, 'mood.nobody')} · ${tempo}`
  if (view.mode === 'club' && view.club) return `${t(lang, leader === view.club ? 'mood.we' : 'mood.they')} · ${tempo}`
  return `${t(lang, 'mood.team', { team: leader.name })} · ${tempo}`
}
