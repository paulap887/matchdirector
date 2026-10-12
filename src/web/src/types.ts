// Mirrors MatchDirector.Contracts and MatchDirector.StatsEngine JSON (camelCase, TimeSpan as "hh:mm:ss.fffffff").

export type PlayerInfo = { playerId: string; name: string; number: number; position: string }
export type TeamInfo = { teamId: string; name: string; players: PlayerInfo[] }
export type MatchInfo = { matchId: string; venue: string; home: TeamInfo; away: TeamInfo }

export type TeamStats = {
  teamId: string
  goals: number
  shots: number
  shotsOnTarget: number
  xg: number
  passes: number
  passesCompleted: number
  passAccuracy: number
  possession: number
  progressivePasses: number
  tackles: number
  interceptions: number
  pressures: number
  fouls: number
}

export type MatchSnapshot = {
  matchId: string
  matchClock: string
  period: number
  home: TeamStats
  away: TeamStats
  momentum: number
  chaos: number
}

export type Overlay = {
  overlayId: string
  matchId: string
  matchClock: string
  durationMs: number
  type: string
  priority: number
  playerId?: string
  metrics: Record<string, number>
  evidence: string[]
  variants: Record<string, string>
}

export type Mode = 'casual' | 'analyst'

export function clock(timeSpan: string): string {
  const [h, m, s] = timeSpan.split(':')
  const minutes = Number(h) * 60 + Number(m)
  return `${String(minutes).padStart(2, '0')}:${String(Math.floor(Number(s))).padStart(2, '0')}`
}
