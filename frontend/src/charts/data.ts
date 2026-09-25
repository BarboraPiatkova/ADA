import type { HealthStatus } from '../api'

// Data shaping for the charts, kept apart from the components that draw them.

/** Every calendar day from `from` to `to` inclusive (ISO dates); empty when either is missing. */
export function heatmapDays(from: string | null, to: string | null) {
  const days: string[] = []
  if (!from || !to) return days
  for (let d = new Date(`${from}T00:00:00Z`); d <= new Date(`${to}T00:00:00Z`); d.setUTCDate(d.getUTCDate() + 1)) {
    days.push(d.toISOString().slice(0, 10))
  }
  return days
}

export interface HistogramBin {
  from: number
  to: number
  count: number
}

/** Equal-width bins from 0 up to the largest value (or `minimumTop`, if higher), optionally capped. */
export function binValues(values: number[], step: number, minimumTop: number, cap?: number): HistogramBin[] {
  const max = Math.max(minimumTop, ...values, step)
  const top = Math.min(cap ?? Infinity, Math.ceil((max + Number.EPSILON) / step) * step)
  const count = Math.max(1, Math.round(top / step))
  const bins = Array.from({ length: count }, (_, i) => ({ from: i * step, to: (i + 1) * step, count: 0 }))
  for (const v of values) {
    bins[Math.min(count - 1, Math.floor(v / step))].count++
  }
  return bins
}

export interface StatusGroup {
  key: string
  label: string
  counts: Record<HealthStatus, number>
}

export const emptyCounts = (): Record<HealthStatus, number> => ({ Fault: 0, Warning: 0, Ok: 0, Unknown: 0 })

const groupTotal = (g: StatusGroup) => g.counts.Fault + g.counts.Warning + g.counts.Ok + g.counts.Unknown

/**
 * Largest groups first; past `limit`, the smallest fold into one "other" group so the
 * chart stays readable (the table view still lists every group).
 */
export function foldSmallGroups(groups: StatusGroup[], limit: number, otherLabel: string): StatusGroup[] {
  const sorted = [...groups].sort((a, b) => groupTotal(b) - groupTotal(a) || a.label.localeCompare(b.label))
  if (sorted.length <= limit) return sorted
  const other: StatusGroup = { key: '__other', label: otherLabel, counts: emptyCounts() }
  for (const g of sorted.slice(limit - 1)) {
    for (const status of Object.keys(g.counts) as HealthStatus[]) other.counts[status] += g.counts[status]
  }
  return [...sorted.slice(0, limit - 1), other]
}
