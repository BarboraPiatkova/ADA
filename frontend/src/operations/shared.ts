import type { StopVisitDwell } from '../api'

// Helpers shared by the dwell views.

/** Lower case without diacritics: the logs write "Svratecka", people type "Svratecká". */
export function normalize(text: string) {
  return text.normalize('NFD').replace(/\p{Diacritic}/gu, '').toLowerCase()
}

/** True when every word of the query appears in one of the texts. */
export function matches(query: string, ...texts: (string | number | null | undefined)[]) {
  const words = normalize(query).split(/\s+/).filter(Boolean)
  if (words.length === 0) return true
  const haystack = normalize(texts.filter((t) => t !== null && t !== undefined).join(' '))
  return words.every((w) => haystack.includes(w))
}

/** ISO local date-time → its calendar day ("2022-08-01"). */
export const dayOf = (iso: string) => iso.slice(0, 10)

/** Extra-dwell classes for the map: upper bounds in seconds, one petrol step each. */
export const EXCESS_BINS = [0, 10, 20, 40] as const

/** 1..5: which step of the map ramp an extra dwell falls in. */
export function excessStep(seconds: number) {
  const i = EXCESS_BINS.findIndex((upper) => seconds <= upper)
  return (i === -1 ? EXCESS_BINS.length : i) + 1
}

export { stopLabel } from '../map/directions'

/** What the reader is looking at in detail: one stop, or one vehicle's day. */
export type DwellDetail = { kind: 'stop'; code: number } | { kind: 'vehicle'; vehicle: number; day: string; at?: string }

/** Monday-first weekday of a local ISO date-time (0 = Monday). */
export const weekdayOf = (iso: string) => (new Date(iso).getDay() + 6) % 7
export const hourOf = (iso: string) => new Date(iso).getHours()

export function median(values: number[]) {
  const sorted = [...values].sort((a, b) => a - b)
  const mid = Math.floor(sorted.length / 2)
  return sorted.length % 2 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2
}

/** One hour of one weekday at a stop. */
export interface HeatCell {
  weekday: number
  hour: number
  count: number
  median: number
}

export function heatCells(visits: StopVisitDwell[]): HeatCell[] {
  const groups = new Map<string, number[]>()
  for (const v of visits) {
    const key = `${weekdayOf(v.arrival)}-${hourOf(v.arrival)}`
    const list = groups.get(key) ?? []
    list.push(v.dwellSeconds)
    groups.set(key, list)
  }
  return [...groups.entries()].map(([key, dwells]) => {
    const [weekday, hour] = key.split('-').map(Number)
    return { weekday, hour, count: dwells.length, median: median(dwells) }
  })
}
