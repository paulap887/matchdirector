import { useEffect, useState } from 'react'
import { isLang, type Lang } from './i18n'
import type { MetricKey, Mode } from './types'

export type Prefs = { mode: Mode; lang: Lang; clubId?: string; playerId?: string; metric?: MetricKey }

const KEY = 'matchdirector.prefs'

const MODES: Mode[] = ['casual', 'analyst', 'club', 'player']

/** Saved preferences, overridden by a shareable link such as ?mode=club&club=RDM&lang=fr or ?mode=player&player=KBY-09. */
function load(): Prefs {
  let prefs: Prefs = { mode: 'casual', lang: 'en' }
  try {
    const saved = localStorage.getItem(KEY)
    if (saved) prefs = { ...prefs, ...JSON.parse(saved) }
    if (!isLang(prefs.lang)) prefs.lang = 'en'
  } catch {
    // storage unavailable (private mode, blocked cookies): fall back to defaults
  }

  const url = new URLSearchParams(window.location.search)
  const mode = url.get('mode') as Mode | null
  if (mode && MODES.includes(mode)) prefs.mode = mode
  if (url.get('club')) prefs.clubId = url.get('club')!
  if (url.get('player')) prefs.playerId = url.get('player')!
  if (url.get('metric')) prefs.metric = url.get('metric') as MetricKey
  const lang = url.get('lang')
  if (isLang(lang)) prefs.lang = lang
  return prefs
}

/** Viewer preferences, remembered in this browser only. */
export function usePrefs(): [Prefs, (change: Partial<Prefs>) => void] {
  const [prefs, setPrefs] = useState<Prefs>(load)
  useEffect(() => {
    try {
      localStorage.setItem(KEY, JSON.stringify(prefs))
    } catch {
      // ignore: preferences just won't persist
    }
  }, [prefs])
  return [prefs, (change) => setPrefs((p) => ({ ...p, ...change }))]
}
