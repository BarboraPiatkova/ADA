import { useTranslation } from 'react-i18next'
import type { HealthStatus, VehicleHealth } from '../api'
import { ChartFigure } from '../charts/ChartFigure'
import { foldSmallGroups, type HistogramBin, type StatusGroup } from '../charts/data'
import { FleetHeatmap, HeatmapLegend, HeatmapTable } from '../charts/FleetHeatmap'
import { Histogram, HistogramTable } from '../charts/Histogram'
import { StatusBars, StatusBarsTable, StatusLegend } from '../charts/StatusBars'
import { Skeleton, SkeletonScreen } from '../ui/Skeleton'
import { SegmentedItem, SegmentedRoot } from '../ui/Segmented'
import type { Filters } from './filters'
import type { GroupBy } from './useHealthAnalysis'
import type { DailyIndex, Metric } from './metrics'

/** The three analysis charts; clicking a mark filters the whole screen (cross-filtering). */
export function AnalysisCharts({
  vehicles,
  days,
  daily,
  dailyPending,
  metric,
  formatMetric,
  thresholds,
  histogramBins,
  statusGroups,
  groupBy,
  onGroupByChange,
  filters,
  setFilters,
}: {
  /** In the table's order, every filtered row (not just the page). */
  vehicles: VehicleHealth[]
  days: string[]
  daily: DailyIndex
  dailyPending: boolean
  metric: Metric
  formatMetric: (value: number) => string
  thresholds: { warning: number; fault?: number } | undefined
  histogramBins: HistogramBin[]
  statusGroups: StatusGroup[]
  groupBy: GroupBy
  onGroupByChange: (groupBy: GroupBy) => void
  filters: Filters
  setFilters: (update: (f: Filters) => Filters) => void
}) {
  const { t } = useTranslation()
  const metricName = t(`charts.metrics.${metric.id}`)

  const toggleRange = (bin: { from: number; to: number }, isLast: boolean) =>
    setFilters((f) =>
      f.range?.metric === metric.id && f.range.from === bin.from
        ? { ...f, range: undefined }
        : { ...f, range: { metric: metric.id, from: bin.from, to: isLast ? Infinity : bin.to } },
    )
  const toggleGroup = (key: string) =>
    setFilters((f) => (groupBy === 'model' ? { ...f, model: f.model === key ? undefined : key } : { ...f, firmware: f.firmware === key ? undefined : key }))
  const toggleSegment = (key: string, status: HealthStatus) =>
    setFilters((f) =>
      groupBy === 'firmware'
        ? { ...f, firmware: f.firmware === key ? undefined : key }
        : f.model === key && f.status === status
          ? { ...f, model: undefined, status: undefined }
          : { ...f, model: key, status },
    )

  return (
    <section className="mb-7 flex flex-col gap-4" aria-label={t('charts.analysis')}>
      <ChartFigure
        title={t('charts.heatmapTitle', { metric: metricName })}
        subtitle={t('charts.heatmapSubtitle')}
        legend={<HeatmapLegend metric={metric} formatValue={formatMetric} />}
        chart={
          // Until the per-day data arrives, every cell would read as "no data" — misleading.
          dailyPending ? (
            <SkeletonScreen label={t('app.loading')}>
              <Skeleton className="h-[180px] w-full" />
            </SkeletonScreen>
          ) : (
            <FleetHeatmap
              vehicles={vehicles}
              days={days}
              daily={daily}
              metric={metric}
              formatValue={formatMetric}
              onSelectVehicle={(id) => setFilters((f) => ({ ...f, search: String(id) }))}
            />
          )
        }
        table={<HeatmapTable vehicles={vehicles} days={days} daily={daily} metric={metric} formatValue={formatMetric} />}
      />
      <div className="grid grid-cols-1 gap-4 md:grid-cols-[repeat(auto-fit,minmax(420px,1fr))]">
        <ChartFigure
          title={t('charts.histogramTitle', { metric: metricName })}
          subtitle={t(thresholds ? 'charts.histogramSubtitle' : 'charts.histogramSubtitleNoThresholds')}
          chart={
            <Histogram
              bins={histogramBins}
              thresholds={thresholds}
              formatValue={formatMetric}
              countLabel={(count) => t('charts.vehicles', { count })}
              selectedFrom={filters.range?.metric === metric.id ? filters.range.from : undefined}
              onSelect={toggleRange}
            />
          }
          table={<HistogramTable bins={histogramBins} formatValue={formatMetric} countHeader={t('charts.vehiclesHeader')} />}
        />
        <ChartFigure
          title={t(groupBy === 'model' ? 'charts.statusByModel' : 'charts.statusByFirmware')}
          subtitle={t(groupBy === 'model' ? 'charts.statusByModelSubtitle' : 'charts.statusByFirmwareSubtitle')}
          controls={
            <SegmentedRoot type="single" value={groupBy} aria-label={t('charts.groupBy')} onValueChange={(value) => value && onGroupByChange(value as GroupBy)}>
              <SegmentedItem value="model">{t('charts.byModel')}</SegmentedItem>
              <SegmentedItem value="firmware">{t('charts.byFirmware')}</SegmentedItem>
            </SegmentedRoot>
          }
          legend={<StatusLegend />}
          chart={
            <StatusBars
              groups={foldSmallGroups(statusGroups, 10, t('charts.other'))}
              countLabel={(count) => t(groupBy === 'model' ? 'charts.vehicles' : 'charts.devices', { count })}
              selectedKey={groupBy === 'model' ? filters.model : filters.firmware}
              selectedStatus={groupBy === 'model' ? filters.status : undefined}
              onSelectGroup={toggleGroup}
              onSelectSegment={toggleSegment}
            />
          }
          table={<StatusBarsTable groups={statusGroups} groupHeader={t(groupBy === 'model' ? 'charts.model' : 'charts.firmware')} />}
        />
      </div>
    </section>
  )
}
