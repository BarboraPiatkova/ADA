import type { HealthStatus, VehicleHealth } from '../api'
import { METRICS, type MetricId } from './metrics'

/**
 * Everything that narrows the device-health screen, in one place. Charts set filters by
 * being clicked (cross-filtering, as in Power BI); the filter row and the chips above the
 * charts show and clear them.
 */
export interface Filters {
  status?: HealthStatus
  traction?: string
  model?: string
  firmware?: string
  /** A histogram bin: vehicles whose whole-period value of `metric` lies in [from, to). */
  range?: { metric: MetricId; from: number; to: number }
  search: string
}

export type FilterKey = Exclude<keyof Filters, 'search'> | 'search'

export const NO_FILTERS: Filters = { search: '' }

export function hasFilters(f: Filters) {
  return Boolean(f.status || f.traction || f.model || f.firmware || f.range || f.search.trim())
}

/**
 * Whether a vehicle passes the filters. `except` leaves out dimensions: a chart is fed
 * data filtered by everything except its own selection, so it keeps showing the
 * alternatives (dimmed) instead of collapsing to the one item picked.
 */
export function matches(v: VehicleHealth, f: Filters, searchText: (v: VehicleHealth) => string, except: FilterKey[] = []) {
  const applies = (key: FilterKey) => !except.includes(key)
  if (applies('status') && f.status && v.status !== f.status) return false
  if (applies('traction') && f.traction && v.traction !== f.traction) return false
  if (applies('model') && f.model !== undefined && (v.model ?? '') !== f.model) return false
  if (applies('firmware') && f.firmware !== undefined && !v.devices.some((d) => (d.firmwareVersion ?? '') === f.firmware)) return false
  if (applies('range') && f.range) {
    const value = METRICS[f.range.metric].vehicle(v)
    if (value === null || value < f.range.from || value >= f.range.to) return false
  }
  if (applies('search') && f.search.trim() && !searchText(v).includes(f.search.trim().toLowerCase())) return false
  return true
}
