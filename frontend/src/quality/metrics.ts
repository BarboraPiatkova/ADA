import type { HealthThresholds, VehicleDay, VehicleHealth } from '../api'

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

export const METRICS: Record<MetricId, Metric> = {
  negative: {
    id: 'negative',
    kind: 'share',
    day: (d) => d.negativeOccupancyShare,
    vehicle: (v) => v.negativeOccupancyShare,
    bins: [0.05, 0.1, 0.2, 0.3, 0.4, Infinity],
    histogramStep: 0.05,
    thresholds: (t) => ({ warning: t.negativeOccupancyWarning, fault: t.negativeOccupancyFault }),
  },
  flagged: {
    id: 'flagged',
    kind: 'share',
    day: (d) => d.flaggedStopShare,
    vehicle: (v) => v.flaggedStopShare,
    bins: [0.05, 0.1, 0.2, 0.3, 0.4, Infinity],
    histogramStep: 0.05,
    thresholds: (t) => ({ warning: t.flaggedStopsWarning }),
  },
  imbalance: {
    id: 'imbalance',
    kind: 'share',
    day: (d) => d.imbalance,
    vehicle: (v) => v.imbalance,
    bins: [0.05, 0.1, 0.2, 0.3, 0.4, Infinity],
    histogramStep: 0.05,
    thresholds: (t) => ({ warning: t.imbalanceWarning, fault: t.imbalanceFault }),
  },
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
