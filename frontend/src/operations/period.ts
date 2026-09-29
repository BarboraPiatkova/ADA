import { useSyncExternalStore } from 'react'
import type { DayRange } from '../ui/DateRangePicker'

// One period for all the statistics screens: choosing days on one tab keeps them on the others.
let current: DayRange | null = null
const listeners = new Set<() => void>()

function setPeriod(period: DayRange | null) {
  current = period
  listeners.forEach((listener) => listener())
}

function subscribe(listener: () => void) {
  listeners.add(listener)
  return () => listeners.delete(listener)
}

/** The chosen period (null = every day) and its setter, shared by the statistics screens. */
export function usePeriod() {
  return [useSyncExternalStore(subscribe, () => current), setPeriod] as const
}
