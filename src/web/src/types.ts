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

export type PlayerStats = {
  playerId: string
  teamId: string
  passes: number
  passesCompleted: number
  progressivePasses: number
  shots: number
  goals: number
  xg: number
  distanceCarried: number
  tackles: number
  interceptions: number
  fastestShot?: number
}

export type MatchSnapshot = {
  matchId: string
  matchClock: string
  period: number
  home: TeamStats
  away: TeamStats
  momentum: number
  chaos: number
  players: PlayerStats[]
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
  participants?: string[]
  trace?: AgentStep[]
}

/** One step in producing an overlay: an agent or a code check. */
export type AgentStep = { agent: string; model: string; outcome: string; durationMs: number; detail?: string }

export type Review = 'approved' | 'corrected' | 'pending' | 'none'

/** Where an overlay is in the review lane, from its agent trace. */
export function reviewOf(overlay: Overlay): Review {
  const critic = overlay.trace?.find((s) => s.agent === 'Verifier critic')
  if (critic) return critic.outcome === 'approved' ? 'approved' : 'corrected'
  return overlay.priority >= 45 && overlay.variants.source === 'agent' ? 'pending' : 'none'
}

export type Mode = 'casual' | 'analyst' | 'club' | 'player'

export type MetricKey = 'possession' | 'xg' | 'shots' | 'passAccuracy' | 'progressivePasses' | 'momentum' | 'chaos'

export const LABELS: Record<string, string> = {
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

export function clock(timeSpan: string): string {
  const [h, m, s] = timeSpan.split(':')
  const minutes = Number(h) * 60 + Number(m)
  return `${String(minutes).padStart(2, '0')}:${String(Math.floor(Number(s))).padStart(2, '0')}`
}
