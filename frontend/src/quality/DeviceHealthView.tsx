import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useTable } from '@tanstack/react-table'
import { Fragment, useMemo, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { DeviceHealthReport, VehicleDay } from '../api'
import { heatmapDays } from '../charts/data'
import { useFormat } from '../i18n/format'
import { dailyQualityQuery, deviceHealthQuery } from '../queries'
import { cn } from '../ui/cn'
import { Empty } from '../ui/Empty'
import { Pagination } from '../ui/Pagination'
import { QueryState } from '../ui/QueryState'
import { NUM, TABLE, TD, WRAP } from '../ui/table'
import { ActiveFilters } from './ActiveFilters'
import { AnalysisCharts } from './AnalysisCharts'
import { buildVehicleColumns, VEHICLE_NUMERIC, vehicleFeatures } from './columns'
import { DeviceTable } from './DeviceTable'
import { SortableHeader } from './SortableHeader'
import { FilterBar } from './FilterBar'
import { NO_FILTERS, type Filters } from './filters'
import { FleetStatus } from './FleetStatus'
import { HealthSkeleton } from './HealthSkeleton'
import { formatMetricValue, indexDaily, METRICS, type MetricId } from './metrics'
import { RulesPanel } from './RulesPanel'
import { useReportPeriod } from '../operations/period'
import { useHealthAnalysis, type GroupBy } from './useHealthAnalysis'
import { FLAG_EDGE } from '../operations/flagEdge'
import { FlagLegend } from '../operations/RowFlag'

const EMPTY_DAYS: VehicleDay[] = []

export function DeviceHealthView() {
  const { t } = useTranslation()
  const { period, isAll } = useReportPeriod()
  // Changing the days keeps the previous report on screen until the next one arrives.
  const report = useQuery({ ...deviceHealthQuery(period), placeholderData: keepPreviousData })
  const daily = useQuery({ ...dailyQualityQuery(period), placeholderData: keepPreviousData })
  return (
    <QueryState query={report} loading={t('health.loading')} skeleton={<HealthSkeleton label={t('health.loading')} />}>
      {(data) =>
        data.vehicles.length === 0 && isAll ? (
          <Empty>{t('health.empty')}</Empty>
        ) : (
          // The heatmap fills in when the per-day data arrives; the rest doesn't wait for it.
          <HealthScreen report={data} daily={daily.data ?? EMPTY_DAYS} dailyPending={daily.isPending} />
        )
      }
    </QueryState>
  )
}

function HealthScreen({ report, daily, dailyPending }: { report: DeviceHealthReport; daily: VehicleDay[]; dailyPending: boolean }) {
  const { t, i18n } = useTranslation()
  const format = useFormat()
  const [filters, setFilters] = useState<Filters>(NO_FILTERS)
  const [metricId, setMetricId] = useState<MetricId>('negative')
  const [groupBy, setGroupBy] = useState<GroupBy>('model')
  const tableTop = useRef<HTMLDivElement>(null)
  const set = (patch: Partial<Filters>) => setFilters((f) => ({ ...f, ...patch }))

  const analysis = useHealthAnalysis(report, filters, metricId, groupBy)
  const columns = useMemo(
    () => buildVehicleColumns(t, report.thresholds, format),
    // eslint-disable-next-line react-hooks/exhaustive-deps -- labels change with the language
    [report.thresholds, i18n.resolvedLanguage],
  )
  const table = useTable({
    features: vehicleFeatures,
    columns,
    data: analysis.filtered,
    getRowId: (v) => String(v.vehicleId),
    getRowCanExpand: () => true,
    initialState: { sorting: [{ id: 'status', desc: false }], pagination: { pageIndex: 0, pageSize: 25 } },
    // A new filter result or sort sends the reader back to page 1 (TanStack's default reset).
  })

  // The table body shows one page; the charts see every filtered, sorted row, so paging
  // never changes what the analysis says.
  const sortedRows = table.getPrePaginatedRowModel().rows
  const visible = useMemo(() => sortedRows.map((row) => row.original), [sortedRows])
  const { pageIndex, pageSize } = table.state.pagination
  const dailyIndex = useMemo(() => indexDaily(daily), [daily])
  const days = useMemo(() => heatmapDays(report.from, report.to), [report.from, report.to])
  const tractions = useMemo(() => [...new Set(report.vehicles.map((v) => v.traction).filter((x): x is string => x !== null))].sort(), [report.vehicles])
  const deviceTotal = report.vehicles.reduce((n, v) => n + v.devices.length, 0)
  const formatMetric = (value: number) => formatMetricValue(analysis.metric, format, value)

  return (
    <div className="flex-1 overflow-y-auto p-4 md:px-7 md:pt-6 md:pb-10">
      <header>
        <h1 className="mb-2 text-2xl">{t('health.title')}</h1>
        <dl className="flex flex-wrap gap-x-7 gap-y-1.5 [&>div]:flex [&>div]:items-baseline [&>div]:gap-2 [&_dd]:font-semibold [&_dt]:text-ink-2">
          <div>
            <dt>{t('health.facts.period')}</dt>
            <dd>
              {report.from ? format.date(report.from) : '?'} – {report.to ? format.date(report.to) : '?'}
            </dd>
          </div>
          <div>
            <dt>{t('health.facts.vehicles')}</dt>
            <dd>{format.number(report.vehicles.length)}</dd>
          </div>
          <div>
            <dt>{t('health.facts.devices')}</dt>
            <dd>{format.number(deviceTotal)}</dd>
          </div>
        </dl>
        <p className="mt-1.5 max-w-[72ch] text-sm text-ink-2">
          {t('health.note')} <RulesPanel th={report.thresholds} />
        </p>
      </header>

      <FleetStatus counts={analysis.counts} selected={filters.status} onSelect={(status) => set({ status })} />
      <FilterBar filters={filters} onChange={set} tractions={tractions} metricId={metricId} onMetricChange={setMetricId} days={report.days} format={format} />
      <ActiveFilters filters={filters} onChange={setFilters} formatRange={(id, value) => formatMetricValue(METRICS[id], format, value)} />

      <AnalysisCharts
        vehicles={visible}
        days={days}
        daily={dailyIndex}
        dailyPending={dailyPending}
        metric={analysis.metric}
        formatMetric={formatMetric}
        thresholds={analysis.thresholds}
        histogramBins={analysis.histogramBins}
        statusGroups={analysis.statusGroups}
        groupBy={groupBy}
        onGroupByChange={setGroupBy}
        filters={filters}
        setFilters={setFilters}
      />

      <div className="mb-2.5 flex items-baseline gap-3">
        <h2 className="text-xl">{t('health.vehiclesTitle')}</h2>
        <span className="text-xs text-ink-2">{t('health.shown', { count: sortedRows.length })}</span>
      </div>

      <div className="overflow-x-auto rounded-[10px] border border-rule" ref={tableTop}>
        <table className={TABLE}>
          <thead>
            {table.getHeaderGroups().map((group) => (
              <tr key={group.id}>
                {group.headers.map((header) => (
                  <SortableHeader
                    key={header.id}
                    sorted={header.column.getIsSorted()}
                    canSort={header.column.getCanSort()}
                    onSort={header.column.getToggleSortingHandler()}
                    numeric={VEHICLE_NUMERIC.has(header.column.id)}
                  >
                    <table.FlexRender header={header} />
                  </SortableHeader>
                ))}
              </tr>
            ))}
          </thead>
          <tbody>
            {table.getRowModel().rows.map((row) => (
              <Fragment key={row.id}>
                <tr
                  className="cursor-pointer hover:[&>td]:bg-surface aria-expanded:[&>td]:bg-route-soft"
                  onClick={() => row.toggleExpanded()}
                  aria-expanded={row.getIsExpanded()}
                >
                  {row.getAllCells().map((cell, i) => (
                    <td
                      key={cell.id}
                      className={cn(
                        TD,
                        VEHICLE_NUMERIC.has(cell.column.id) && NUM,
                        cell.column.id === 'reasons' && WRAP,
                        // A faulty or warned vehicle is marked at the row's edge, as on every table.
                        i === 0 && (row.original.status === 'Fault' || row.original.status === 'Warning') && FLAG_EDGE[row.original.status],
                      )}
                    >
                      <table.FlexRender cell={cell} />
                    </td>
                  ))}
                </tr>
                {row.getIsExpanded() && (
                  <tr>
                    <td colSpan={row.getAllCells().length} className="bg-route-soft pt-1 pr-3 pb-4 pl-12">
                      <DeviceTable devices={row.original.devices} />
                    </td>
                  </tr>
                )}
              </Fragment>
            ))}
          </tbody>
        </table>
      </div>
      <FlagLegend
        warning={t('flags.healthLegendWarning', {
          imbalance: format.percentWhole(report.thresholds.imbalanceWarning),
          negative: format.percentWhole(report.thresholds.negativeOccupancyWarning),
          flagged: format.percentWhole(report.thresholds.flaggedStopsWarning),
        })}
        fault={t('flags.healthLegendFault', {
          imbalance: format.percentWhole(report.thresholds.imbalanceFault),
          negative: format.percentWhole(report.thresholds.negativeOccupancyFault),
        })}
      />

      <Pagination
        pageIndex={pageIndex}
        pageSize={pageSize}
        rowCount={sortedRows.length}
        onPageChange={(page) => {
          table.setPageIndex(page)
          tableTop.current?.scrollIntoView({ block: 'nearest' })
        }}
        onPageSizeChange={(size) => table.setPageSize(size)}
      />
    </div>
  )
}
