import { useQuery } from '@tanstack/react-query'
import {
  columnFilteringFeature,
  createColumnHelper,
  createExpandedRowModel,
  createFilteredRowModel,
  createSortedRowModel,
  filterFn_equals,
  functionalUpdate,
  globalFilteringFeature,
  rowExpandingFeature,
  rowSortingFeature,
  tableFeatures,
  useTable,
  type ColumnFiltersState,
  type FilterFn,
  type SortFn,
} from '@tanstack/react-table'
import type { TFunction } from 'i18next'
import { Collapsible, ToggleGroup } from 'radix-ui'
import { Fragment, useMemo, useState } from 'react'
import { Trans, useTranslation } from 'react-i18next'
import type { DeviceHealth, DeviceHealthReport, HealthReason, HealthStatus, HealthThresholds, VehicleDay, VehicleHealth } from '../api'
import { ChartFigure } from '../charts/ChartFigure'
import { binValues, emptyCounts, foldSmallGroups, heatmapDays, type StatusGroup } from '../charts/data'
import { FleetHeatmap, HeatmapLegend, HeatmapTable } from '../charts/FleetHeatmap'
import { Histogram, HistogramTable } from '../charts/Histogram'
import { StatusBars, StatusBarsTable, StatusLegend } from '../charts/StatusBars'
import { useFormat } from '../i18n/format'
import { dailyQualityQuery, deviceHealthQuery } from '../queries'
import { Hint } from '../ui/Hint'
import { StatusIcon, TractionIcon } from '../ui/icons'
import { QueryState } from '../ui/QueryState'
import { Select } from '../ui/Select'
import { METRIC_ORDER, METRICS, type MetricId } from './metrics'

type Format = ReturnType<typeof useFormat>

const STATUS_ORDER: HealthStatus[] = ['Fault', 'Warning', 'Ok', 'Unknown']

// Only the features these tables use are registered (TanStack Table v9 is opt-in per feature).
const vehicleFeatures = tableFeatures({
  rowSortingFeature,
  sortedRowModel: createSortedRowModel(),
  columnFilteringFeature,
  globalFilteringFeature,
  filteredRowModel: createFilteredRowModel(),
  filterFns: { equals: filterFn_equals },
  rowExpandingFeature,
  expandedRowModel: createExpandedRowModel(),
})
const deviceFeatures = tableFeatures({ rowSortingFeature, sortedRowModel: createSortedRowModel() })

const vehicleColumns = createColumnHelper<typeof vehicleFeatures, VehicleHealth>()
const deviceColumns = createColumnHelper<typeof deviceFeatures, DeviceHealth>()

/** Worst first when sorted ascending. */
const byStatus: SortFn<any, any> = (a, b, id) =>
  STATUS_ORDER.indexOf(a.getValue<HealthStatus>(id)) - STATUS_ORDER.indexOf(b.getValue<HealthStatus>(id))

const EMPTY_DAYS: VehicleDay[] = []

/**
 * Search matches a vehicle's number, model, traction (as stored and as shown) and the
 * firmware of its devices — whatever a dispatcher is likely to type.
 */
function searchText(t: TFunction, v: VehicleHealth) {
  return [v.vehicleId, v.model ?? '', v.traction ?? '', v.traction ? tractionLabel(t, v.traction) : '', ...v.devices.map((d) => d.firmwareVersion ?? '')]
    .join(' ')
    .toLowerCase()
}

/** Phrases reason codes from the API in the current language, one per line. */
function formatReasons(t: TFunction, reasons: HealthReason[]) {
  if (reasons.length === 0) return <span className="muted">—</span>
  return (
    <ul className="reason-list">
      {reasons.map((r) => (
        <li key={r.code}>{t(`health.reasons.${r.code}`, { count: r.value ?? 0, value: r.value ?? 0 })}</li>
      ))}
    </ul>
  )
}

/** Traction comes from the vehicle's own log (in Czech); show it in the UI language. */
function tractionLabel(t: TFunction, traction: string) {
  return traction ? t(`health.tractions.${traction}` as 'health.tractions.tramvaj', { defaultValue: traction }) : '—'
}

function StatusPill({ status }: { status: HealthStatus }) {
  const { t } = useTranslation()
  return (
    <span className={`pill pill-${status.toLowerCase()}`}>
      <StatusIcon status={status} size={14} />
      {t(`health.status.${status}`)}
    </span>
  )
}

/** A share cell coloured by the same thresholds the backend used. */
function Share({ value, warning, fault, format }: { value: number | undefined; warning: number; fault?: number; format: Format }) {
  if (value === undefined) return <span className="muted">—</span>
  const level = fault !== undefined && value >= fault ? 'fault' : value >= warning ? 'warning' : ''
  return <span className={level ? `share share-${level}` : 'share'}>{format.percent(value)}</span>
}

function HeaderHint({ label, hint }: { label: string; hint: string }) {
  return (
    <Hint text={hint}>
      <span className="has-hint">{label}</span>
    </Hint>
  )
}

function buildVehicleColumns(t: TFunction, th: HealthThresholds, format: Format) {
  return vehicleColumns.columns([
    vehicleColumns.accessor('status', {
      header: t('health.columns.status'),
      cell: (info) => <StatusPill status={info.getValue()} />,
      sortFn: byStatus,
      filterFn: 'equals',
    }),
    vehicleColumns.accessor('vehicleId', { header: t('health.columns.vehicle'), cell: (info) => <span className="vehicle-number">{info.getValue()}</span> }),
    vehicleColumns.accessor((v) => v.traction ?? '', {
      id: 'traction',
      header: t('health.columns.traction'),
      filterFn: 'equals',
      cell: (info) => (
        <span className="traction">
          <TractionIcon traction={info.getValue()} />
          <span>
            {tractionLabel(t, info.getValue())} <span className="model">{info.row.original.model ?? ''}</span>
          </span>
        </span>
      ),
    }),
    vehicleColumns.accessor('boardings', { header: t('health.columns.boardings'), cell: (info) => format.number(info.getValue()) }),
    vehicleColumns.accessor('alightings', { header: t('health.columns.alightings'), cell: (info) => format.number(info.getValue()) }),
    vehicleColumns.accessor((v) => v.imbalance ?? undefined, {
      id: 'imbalance',
      header: () => <HeaderHint label={t('health.columns.imbalance')} hint={t('health.hints.imbalance')} />,
      sortUndefined: 'last',
      cell: (info) => <Share value={info.getValue()} warning={th.imbalanceWarning} fault={th.imbalanceFault} format={format} />,
    }),
    vehicleColumns.accessor((v) => v.negativeOccupancyShare ?? undefined, {
      id: 'negative',
      header: () => <HeaderHint label={t('health.columns.negative')} hint={t('health.hints.negative')} />,
      sortUndefined: 'last',
      cell: (info) => <Share value={info.getValue()} warning={th.negativeOccupancyWarning} fault={th.negativeOccupancyFault} format={format} />,
    }),
    vehicleColumns.accessor((v) => v.flaggedStopShare ?? undefined, {
      id: 'flagged',
      header: () => <HeaderHint label={t('health.columns.flagged')} hint={t('health.hints.flagged')} />,
      sortUndefined: 'last',
      cell: (info) => <Share value={info.getValue()} warning={th.flaggedStopsWarning} format={format} />,
    }),
    vehicleColumns.display({
      id: 'reasons',
      header: t('health.columns.reasons'),
      cell: (info) => formatReasons(t, info.row.original.reasons),
    }),
  ])
}

const NUMERIC = new Set(['vehicleId', 'boardings', 'alightings', 'imbalance', 'negative', 'flagged'])

function buildDeviceColumns(t: TFunction, format: Format) {
  return deviceColumns.columns([
    deviceColumns.accessor('status', { header: t('health.columns.status'), cell: (info) => <StatusPill status={info.getValue()} />, sortFn: byStatus }),
    deviceColumns.accessor('deviceNumber', { header: t('health.columns.device'), cell: (info) => <strong>{info.getValue()}</strong> }),
    deviceColumns.accessor((d) => d.firmwareVersion ?? '', { id: 'firmware', header: t('health.columns.firmware'), cell: (info) => info.getValue() || '—' }),
    deviceColumns.accessor('stopsCounted', { header: t('health.columns.stops'), cell: (info) => format.number(info.getValue()) }),
    deviceColumns.accessor('boardings', { header: t('health.columns.boardings'), cell: (info) => format.number(info.getValue()) }),
    deviceColumns.accessor('alightings', { header: t('health.columns.alightings'), cell: (info) => format.number(info.getValue()) }),
    deviceColumns.accessor('notAliveHeartbeats', {
      header: t('health.columns.notAlive'),
      cell: (info) =>
        info.row.original.heartbeats === 0 ? '—' : `${format.number(info.getValue())} / ${format.number(info.row.original.heartbeats)}`,
    }),
    deviceColumns.accessor('restarts', { header: t('health.columns.restarts'), cell: (info) => format.number(info.getValue()) }),
    deviceColumns.accessor('flaggedStops', { header: t('health.columns.flaggedStops'), cell: (info) => format.number(info.getValue()) }),
    deviceColumns.display({
      id: 'reasons',
      header: t('health.columns.reasons'),
      cell: (info) => formatReasons(t, info.row.original.reasons),
    }),
  ])
}
const DEVICE_NUMERIC = new Set(['deviceNumber', 'stopsCounted', 'boardings', 'alightings', 'notAliveHeartbeats', 'restarts', 'flaggedStops'])

function SortIndicator({ sorted }: { sorted: false | 'asc' | 'desc' }) {
  return <span className="sort-indicator">{sorted === 'asc' ? ' ▴' : sorted === 'desc' ? ' ▾' : ''}</span>
}

function DeviceTable({ devices }: { devices: DeviceHealth[] }) {
  const { t, i18n } = useTranslation()
  const format = useFormat()
  // eslint-disable-next-line react-hooks/exhaustive-deps -- labels change with the language
  const columns = useMemo(() => buildDeviceColumns(t, format), [i18n.resolvedLanguage])
  const table = useTable({
    features: deviceFeatures,
    columns,
    data: devices,
    initialState: { sorting: [{ id: 'deviceNumber', desc: false }] },
  })

  return (
    <table className="data-table inner">
      <thead>
        {table.getHeaderGroups().map((group) => (
          <tr key={group.id}>
            {group.headers.map((header) => (
              <th key={header.id} className={DEVICE_NUMERIC.has(header.column.id) ? 'num' : ''}>
                {header.column.getCanSort() ? (
                  <button className="sort-button" onClick={header.column.getToggleSortingHandler()}>
                    <table.FlexRender header={header} />
                    <SortIndicator sorted={header.column.getIsSorted()} />
                  </button>
                ) : (
                  <table.FlexRender header={header} />
                )}
              </th>
            ))}
          </tr>
        ))}
      </thead>
      <tbody>
        {table.getRowModel().rows.map((row) => (
          <tr key={row.id}>
            {row.getAllCells().map((cell) => (
              <td key={cell.id} className={`${DEVICE_NUMERIC.has(cell.column.id) ? 'num' : ''}${cell.column.id === 'reasons' ? ' reasons' : ''}`}>
                <table.FlexRender cell={cell} />
              </td>
            ))}
          </tr>
        ))}
      </tbody>
    </table>
  )
}

function Rules({ th }: { th: HealthThresholds }) {
  const { t } = useTranslation()
  const format = useFormat()
  const markup = { strong: <strong />, code: <code /> }
  return (
    <Collapsible.Root className="rules">
      <Collapsible.Trigger className="link-button">{t('health.rules.title')} ▾</Collapsible.Trigger>
      <Collapsible.Content>
        <ul>
          <li>
            <Trans
              i18nKey="health.rules.imbalance"
              values={{ min: th.minPassengersForBalance, warning: format.percent(th.imbalanceWarning), fault: format.percent(th.imbalanceFault) }}
              components={markup}
            />
          </li>
          <li>
            <Trans
              i18nKey="health.rules.negative"
              values={{ warning: format.percent(th.negativeOccupancyWarning), fault: format.percent(th.negativeOccupancyFault) }}
              components={markup}
            />
          </li>
          <li>
            <Trans i18nKey="health.rules.flagged" values={{ warning: format.percent(th.flaggedStopsWarning) }} components={markup} />
          </li>
          <li>
            <Trans i18nKey="health.rules.silent" components={markup} />
          </li>
          <li>{t('health.rules.counts')}</li>
          <li>{t('health.rules.restarts')}</li>
        </ul>
      </Collapsible.Content>
    </Collapsible.Root>
  )
}

function VehicleHealthTable({ report, daily }: { report: DeviceHealthReport; daily: VehicleDay[] }) {
  const { t, i18n } = useTranslation()
  const format = useFormat()
  const [columnFilters, setColumnFilters] = useState<ColumnFiltersState>([])
  const [search, setSearch] = useState('')
  const [metricId, setMetricId] = useState<MetricId>('negative')
  const [groupBy, setGroupBy] = useState<'model' | 'firmware'>('model')
  const metric = METRICS[metricId]

  const searchFn = useMemo<FilterFn<any, VehicleHealth>>(
    () => (row, _columnId, value: string) => searchText(t, row.original).includes(String(value).trim().toLowerCase()),
    // eslint-disable-next-line react-hooks/exhaustive-deps -- depends on the language only
    [i18n.resolvedLanguage],
  )
  const columns = useMemo(
    () => buildVehicleColumns(t, report.thresholds, format),
    // eslint-disable-next-line react-hooks/exhaustive-deps -- labels change with the language
    [report.thresholds, i18n.resolvedLanguage],
  )

  const table = useTable({
    features: vehicleFeatures,
    columns,
    data: report.vehicles,
    getRowId: (v) => String(v.vehicleId),
    getRowCanExpand: () => true,
    initialState: { sorting: [{ id: 'status', desc: false }] },
    state: { columnFilters, globalFilter: search },
    onColumnFiltersChange: (updater) => setColumnFilters((previous) => functionalUpdate(updater, previous)),
    onGlobalFilterChange: (updater) => setSearch((previous) => functionalUpdate(updater, previous) ?? ''),
    globalFilterFn: searchFn,
    // One eligible column is enough: the search function looks at the whole row.
    getColumnCanGlobalFilter: (column) => column.id === 'vehicleId',
  })

  const all = report.vehicles
  const counts = Object.fromEntries(STATUS_ORDER.map((s) => [s, all.filter((v) => v.status === s).length])) as Record<HealthStatus, number>
  const tractions = [...new Set(all.map((v) => v.traction).filter((x): x is string => x !== null))].sort()
  const deviceTotal = all.reduce((n, v) => n + v.devices.length, 0)

  const statusFilter = (columnFilters.find((f) => f.id === 'status')?.value as HealthStatus | undefined) ?? ''
  const tractionFilter = (columnFilters.find((f) => f.id === 'traction')?.value as string | undefined) ?? 'all'
  const shown = table.getRowModel().rows

  // Everything below the filter row sees the same slice: charts follow the table's filters and order.
  const visible = shown.map((row) => row.original)
  const dailyByKey = useMemo(() => new Map(daily.map((d) => [`${d.vehicleId}|${d.day}`, d])), [daily])
  const days = useMemo(() => heatmapDays(report.from, report.to), [report.from, report.to])
  const formatMetric = (value: number) => (metric.kind === 'share' ? format.percentWhole(value) : format.number(Math.round(value)))
  const metricName = t(`charts.metrics.${metricId}`)
  const thresholds = metric.thresholds?.(report.thresholds)
  const histogramBins = binValues(
    visible.map(metric.vehicle).filter((v): v is number => v !== null),
    metric.histogramStep,
    thresholds ? (thresholds.fault ?? thresholds.warning) * 1.2 : 0,
    metric.kind === 'share' ? 1 : undefined,
  )
  const statusGroups: StatusGroup[] = (() => {
    const groups = new Map<string, StatusGroup>()
    const add = (key: string, label: string, status: HealthStatus) => {
      const group = groups.get(key) ?? { key, label, counts: emptyCounts() }
      group.counts[status]++
      groups.set(key, group)
    }
    for (const v of visible) {
      if (groupBy === 'model') add(v.model ?? '', v.model ?? t('charts.unknownModel'), v.status)
      else for (const d of v.devices) add(d.firmwareVersion ?? '', d.firmwareVersion ?? t('charts.unknownFirmware'), d.status)
    }
    return [...groups.values()]
  })()
  const clearFilters = () => {
    setColumnFilters([])
    setSearch('')
  }

  return (
    <div className="page">
      <header className="page-header">
        <h2>{t('health.title')}</h2>
        <dl className="facts">
          <div>
            <dt>{t('health.facts.period')}</dt>
            <dd>
              {report.from ? format.date(report.from) : '?'} – {report.to ? format.date(report.to) : '?'}
            </dd>
          </div>
          <div>
            <dt>{t('health.facts.vehicles')}</dt>
            <dd>{format.number(all.length)}</dd>
          </div>
          <div>
            <dt>{t('health.facts.devices')}</dt>
            <dd>{format.number(deviceTotal)}</dd>
          </div>
        </dl>
        <p className="page-note">{t('health.note')}</p>
      </header>

      <section className="fleet-status" aria-label={t('health.fleetStatus')}>
        {/* Decorative summary of the counts below; the toggle buttons carry the same numbers as text. */}
        <div className={`status-bar${statusFilter ? ' filtered' : ''}`} aria-hidden="true">
          {STATUS_ORDER.filter((s) => counts[s] > 0).map((s) => (
            <span
              key={s}
              className={`status-bar-segment ${s.toLowerCase()}${statusFilter === s ? ' active' : ''}`}
              style={{ flexGrow: counts[s] }}
            />
          ))}
        </div>
        <ToggleGroup.Root
          type="single"
          className="status-filter"
          aria-label={t('health.statusFilter')}
          value={statusFilter}
          onValueChange={(value) => table.getColumn('status')?.setFilterValue(value || undefined)}
        >
          {STATUS_ORDER.map((s) => (
            <ToggleGroup.Item key={s} value={s} className="status-filter-item">
              <span className={`pill pill-${s.toLowerCase()}`}>
                <StatusIcon status={s} size={14} />
                {t(`health.status.${s}`)}
              </span>
              <span className="status-filter-count">{format.number(counts[s])}</span>
            </ToggleGroup.Item>
          ))}
        </ToggleGroup.Root>
      </section>

      <div className="filters" role="search">
        <label className="search">
          <span className="visually-hidden">{t('health.searchLabel')}</span>
          <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden="true">
            <circle cx="11" cy="11" r="7" />
            <path d="m20 20-3.5-3.5" />
          </svg>
          <input type="search" value={search} placeholder={t('health.searchPlaceholder')} onChange={(event) => setSearch(event.target.value)} />
        </label>
        <Select
          label={t('health.traction')}
          value={tractionFilter}
          options={[{ value: 'all', label: t('health.allTractions') }, ...tractions.map((x) => ({ value: x, label: tractionLabel(t, x) }))]}
          onChange={(value) => table.getColumn('traction')?.setFilterValue(value === 'all' ? undefined : value)}
        />
        <Select
          label={t('charts.metric')}
          value={metricId}
          options={METRIC_ORDER.map((id) => ({ value: id, label: t(`charts.metrics.${id}`) }))}
          onChange={(value) => setMetricId(value as MetricId)}
        />
        {(columnFilters.length > 0 || search) && (
          <button className="link-button" onClick={clearFilters}>
            {t('health.clearFilters')}
          </button>
        )}
      </div>

      <section className="analysis" aria-label={t('charts.analysis')}>
        <ChartFigure
          title={t('charts.heatmapTitle', { metric: metricName })}
          subtitle={t('charts.heatmapSubtitle')}
          legend={<HeatmapLegend metric={metric} formatValue={formatMetric} />}
          chart={
            <FleetHeatmap
              vehicles={visible}
              days={days}
              daily={dailyByKey}
              metric={metric}
              formatValue={formatMetric}
              onSelectVehicle={(id) => setSearch(String(id))}
            />
          }
          table={<HeatmapTable vehicles={visible} days={days} daily={dailyByKey} metric={metric} formatValue={formatMetric} />}
        />
        <div className="analysis-grid">
          <ChartFigure
            title={t('charts.histogramTitle', { metric: metricName })}
            subtitle={t(thresholds ? 'charts.histogramSubtitle' : 'charts.histogramSubtitleNoThresholds')}
            chart={
              <Histogram bins={histogramBins} thresholds={thresholds} formatValue={formatMetric} countLabel={(count) => t('charts.vehicles', { count })} />
            }
            table={<HistogramTable bins={histogramBins} formatValue={formatMetric} countHeader={t('charts.vehiclesHeader')} />}
          />
          <ChartFigure
            title={t(groupBy === 'model' ? 'charts.statusByModel' : 'charts.statusByFirmware')}
            subtitle={t(groupBy === 'model' ? 'charts.statusByModelSubtitle' : 'charts.statusByFirmwareSubtitle')}
            controls={
              <ToggleGroup.Root
                type="single"
                className="segmented"
                value={groupBy}
                aria-label={t('charts.groupBy')}
                onValueChange={(value) => value && setGroupBy(value as 'model' | 'firmware')}
              >
                <ToggleGroup.Item value="model" className="segmented-item">
                  {t('charts.byModel')}
                </ToggleGroup.Item>
                <ToggleGroup.Item value="firmware" className="segmented-item">
                  {t('charts.byFirmware')}
                </ToggleGroup.Item>
              </ToggleGroup.Root>
            }
            legend={<StatusLegend />}
            chart={<StatusBars groups={foldSmallGroups(statusGroups, 10, t('charts.other'))} countLabel={(count) => t(groupBy === 'model' ? 'charts.vehicles' : 'charts.devices', { count })} />}
            table={<StatusBarsTable groups={statusGroups} groupHeader={t(groupBy === 'model' ? 'charts.model' : 'charts.firmware')} />}
          />
        </div>
      </section>

      <div className="section-head">
        <h3>{t('health.vehiclesTitle')}</h3>
        <span className="muted small">{t('health.shown', { count: shown.length })}</span>
      </div>

      <div className="table-wrap">
        <table className="data-table">
          <thead>
            {table.getHeaderGroups().map((group) => (
              <tr key={group.id}>
                {group.headers.map((header) => (
                  <th
                    key={header.id}
                    className={NUMERIC.has(header.column.id) ? 'num' : ''}
                    aria-sort={header.column.getIsSorted() === 'asc' ? 'ascending' : header.column.getIsSorted() === 'desc' ? 'descending' : 'none'}
                  >
                    {header.column.getCanSort() ? (
                      <button className="sort-button" onClick={header.column.getToggleSortingHandler()}>
                        <table.FlexRender header={header} />
                        <SortIndicator sorted={header.column.getIsSorted()} />
                      </button>
                    ) : (
                      <table.FlexRender header={header} />
                    )}
                  </th>
                ))}
              </tr>
            ))}
          </thead>
          <tbody>
            {shown.map((row) => (
              <Fragment key={row.id}>
                <tr className="clickable" onClick={() => row.toggleExpanded()} aria-expanded={row.getIsExpanded()}>
                  {row.getAllCells().map((cell) => (
                    <td key={cell.id} className={`${NUMERIC.has(cell.column.id) ? 'num' : ''}${cell.column.id === 'reasons' ? ' reasons' : ''}`}>
                      <table.FlexRender cell={cell} />
                    </td>
                  ))}
                </tr>
                {row.getIsExpanded() && (
                  <tr className="detail-row">
                    <td colSpan={row.getAllCells().length}>
                      <DeviceTable devices={row.original.devices} />
                    </td>
                  </tr>
                )}
              </Fragment>
            ))}
          </tbody>
        </table>
      </div>

      <Rules th={report.thresholds} />
    </div>
  )
}

export function DeviceHealthView() {
  const { t } = useTranslation()
  const report = useQuery(deviceHealthQuery)
  const daily = useQuery(dailyQualityQuery)
  return (
    <QueryState query={report} loading={t('health.loading')}>
      {(data) =>
        data.vehicles.length === 0 ? (
          <p className="empty">{t('health.empty')}</p>
        ) : (
          // The heatmap fills in when the per-day data arrives; the rest doesn't wait for it.
          <VehicleHealthTable report={data} daily={daily.data ?? EMPTY_DAYS} />
        )
      }
    </QueryState>
  )
}
