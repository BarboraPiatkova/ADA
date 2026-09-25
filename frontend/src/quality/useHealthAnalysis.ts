import { useMemo } from 'react'
import { useTranslation } from 'react-i18next'
import type { DeviceHealthReport, HealthStatus, VehicleHealth } from '../api'
import { binValues, emptyCounts, type StatusGroup } from '../charts/data'
import { STATUS_ORDER } from '../ui/status'
import { matches, type FilterKey, type Filters } from './filters'
import { searchText } from './labels'
import { METRICS, type MetricId } from './metrics'

export type GroupBy = 'model' | 'firmware'

/**
 * Everything the device-health screen derives from the report and the filters, memoised.
 * Cross-filtering as in Power BI: each chart sees the vehicles passing every filter except
 * its own, so it keeps showing the alternatives (dimmed) instead of collapsing to the pick.
 */
export function useHealthAnalysis(report: DeviceHealthReport, filters: Filters, metricId: MetricId, groupBy: GroupBy) {
  const { t, i18n } = useTranslation()
  const vehicles = report.vehicles
  const language = i18n.resolvedLanguage

  // Search text once per vehicle and language, not once per vehicle per filter check.
  const textOf = useMemo(() => {
    const texts = new Map(vehicles.map((v) => [v, searchText(t, v)]))
    return (v: VehicleHealth) => texts.get(v) ?? ''
    // eslint-disable-next-line react-hooks/exhaustive-deps -- t changes with the language
  }, [vehicles, language])

  return useMemo(() => {
    const except = (...keys: FilterKey[]) => vehicles.filter((v) => matches(v, filters, textOf, keys))
    const metric = METRICS[metricId]

    // Status counts react to every filter except status itself (a slicer).
    const forStatus = except('status')
    const counts = Object.fromEntries(STATUS_ORDER.map((s) => [s, forStatus.filter((v) => v.status === s).length])) as Record<HealthStatus, number>

    const thresholds = metric.thresholds?.(report.thresholds)
    const histogramBins = binValues(
      except('range')
        .map(metric.vehicle)
        .filter((v): v is number => v !== null),
      metric.histogramStep,
      thresholds ? (thresholds.fault ?? thresholds.warning) * 1.2 : 0,
      metric.kind === 'share' ? 1 : undefined,
    )

    // The bars keep showing every group (the selected one highlighted), so they ignore their own filters.
    const groups = new Map<string, StatusGroup>()
    const add = (key: string, label: string, status: HealthStatus) => {
      const group = groups.get(key) ?? { key, label, counts: emptyCounts() }
      group.counts[status]++
      groups.set(key, group)
    }
    for (const v of groupBy === 'model' ? except('model', 'status') : except('firmware')) {
      if (groupBy === 'model') add(v.model ?? '', v.model ?? t('charts.unknownModel'), v.status)
      else for (const d of v.devices) add(d.firmwareVersion ?? '', d.firmwareVersion ?? t('charts.unknownFirmware'), d.status)
    }

    return {
      /** Vehicles passing every filter: the table's rows. */
      filtered: vehicles.filter((v) => matches(v, filters, textOf)),
      counts,
      metric,
      thresholds,
      histogramBins,
      statusGroups: [...groups.values()],
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps -- t changes with the language
  }, [vehicles, report.thresholds, filters, metricId, groupBy, textOf, language])
}
