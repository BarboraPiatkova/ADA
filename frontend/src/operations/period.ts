import { useMemo, useSyncExternalStore } from 'react'
import type { DayKind, Period } from '../api'
import type { DayRange } from '../ui/DateRangePicker'

// One period for all the statistics screens: choosing days on one tab keeps them on the others.
let state: { range: DayRange | null; days: DayKind } = { range: null, days: 'all' }
const listeners = new Set<() => void>()

function update(next: Partial<typeof state>) {
  state = { ...state, ...next }
  listeners.forEach((listener) => listener())
}

function subscribe(listener: () => void) {
  listeners.add(listener)
  return () => listeners.delete(listener)
}

const useStore = () => useSyncExternalStore(subscribe, () => state)

/** The chosen days (null = every day) and its setter, shared by the statistics screens. */
export function usePeriod() {
  return [useStore().range, (range: DayRange | null) => update({ range })] as const
}

/** Which days of the week count (all, working days, Saturdays, Sundays), shared by the statistics screens. */
export function useDayKind() {
  return [useStore().days, (days: DayKind) => update({ days })] as const
}

/** The period as the reports take it; `isAll` when nothing narrows it. */
export function useReportPeriod(): { period: Period; isAll: boolean } {
  const { range, days } = useStore()
  return useMemo(
    () => ({ period: { from: range?.from, to: range?.to, days: days === 'all' ? undefined : days }, isAll: range === null && days === 'all' }),
    [range, days],
  )
}

export const DAY_KINDS: DayKind[] = ['all', 'workdays', 'saturday', 'sunday']
