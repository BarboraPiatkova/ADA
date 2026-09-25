import type { HealthThresholds, VehicleDay, VehicleHealth } from '../api'
import type { Format } from '../i18n/format'

// The measures the analysis charts can show. Each knows how to read itself from a
// vehicle-day (heatmap) and from a vehicle's whole period (histogram), how to bin itself
// for the sequential colour scale, and which report thresholds apply to it.

export type MetricId = 'negative' | 'flagged' | 'imbalance' | 'boardings'

export interface Metric {
  id: MetricId
  kind: 'share' | 'count'
  day: (d: VehicleDay) => number | null
  vehicle: (v: VehicleHealth) => number | null
  /** Upper edges of the heatmap colour bins, ascending; the last is Infinity. Six bins ↔ six ramp steps. */
  bins: number[]
  /** Histogram bin width. */
  histogramStep: number
  thresholds?: (t: HealthThresholds) => { warning: number; fault?: number }
}

// The three share metrics differ only in what they read and which thresholds apply.
const SHARE_BINS = [0.05, 0.1, 0.2, 0.3, 0.4, Infinity]

function share(
  id: MetricId,
  read: { day: Metric['day']; vehicle: Metric['vehicle'] },
  thresholds: NonNullable<Metric['thresholds']>,
): Metric {
  return { id, kind: 'share', ...read, bins: SHARE_BINS, histogramStep: 0.05, thresholds }
}

export const METRICS: Record<MetricId, Metric> = {
  negative: share(
    'negative',
    { day: (d) => d.negativeOccupancyShare, vehicle: (v) => v.negativeOccupancyShare },
    (t) => ({ warning: t.negativeOccupancyWarning, fault: t.negativeOccupancyFault }),
  ),
  flagged: share('flagged', { day: (d) => d.flaggedStopShare, vehicle: (v) => v.flaggedStopShare }, (t) => ({ warning: t.flaggedStopsWarning })),
  imbalance: share(
    'imbalance',
    { day: (d) => d.imbalance, vehicle: (v) => v.imbalance },
    (t) => ({ warning: t.imbalanceWarning, fault: t.imbalanceFault }),
  ),
  boardings: {
    id: 'boardings',
    kind: 'count',
    day: (d) => (d.stopSummaries > 0 || d.boardings > 0 ? d.boardings : null),
    vehicle: (v) => v.boardings,
    bins: [250, 500, 1000, 2000, 4000, Infinity],
    histogramStep: 2500,
  },
}

export const METRIC_ORDER: MetricId[] = ['negative', 'flagged', 'imbalance', 'boardings']

/** Index of the colour bin a value falls in (0 = lowest). */
export function binOf(metric: Metric, value: number) {
  const index = metric.bins.findIndex((edge) => value < edge)
  return index === -1 ? metric.bins.length - 1 : index
}

/** A value of this metric as the charts label it: a whole percentage, or a rounded count. */
export function formatMetricValue(metric: Metric, format: Format, value: number) {
  return metric.kind === 'share' ? format.percentWhole(value) : format.number(Math.round(value))
}

/** Per-day records by vehicle and day: the heatmap's lookup. */
export type DailyIndex = Map<string, VehicleDay>

export const dayKey = (vehicleId: number, day: string) => `${vehicleId}|${day}`

export const indexDaily = (daily: VehicleDay[]): DailyIndex => new Map(daily.map((d) => [dayKey(d.vehicleId, d.day), d]))

/** One heatmap cell: the day's record (if the vehicle ran) and the metric's value. */
export function dayValue(daily: DailyIndex, metric: Metric, vehicleId: number, day: string) {
  const record = daily.get(dayKey(vehicleId, day))
  return { record, value: record ? metric.day(record) : null }
}
