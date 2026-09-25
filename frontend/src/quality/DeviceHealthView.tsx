import { useQuery } from '@tanstack/react-query'
import {
  createColumnHelper,
  createExpandedRowModel,
  createPaginatedRowModel,
  createSortedRowModel,
  rowExpandingFeature,
  rowPaginationFeature,
  rowSortingFeature,
  tableFeatures,
  useTable,
  type SortFn,
} from '@tanstack/react-table'
import type { TFunction } from 'i18next'
import { Dialog, ToggleGroup } from 'radix-ui'
import { Fragment, useMemo, useRef, useState } from 'react'
import { Trans, useTranslation } from 'react-i18next'
import type { DeviceHealth, DeviceHealthReport, HealthReason, HealthStatus, HealthThresholds, VehicleDay, VehicleHealth } from '../api'
import { ChartFigure } from '../charts/ChartFigure'
import { binValues, emptyCounts, foldSmallGroups, heatmapDays, type StatusGroup } from '../charts/data'
import { FleetHeatmap, HeatmapLegend, HeatmapTable } from '../charts/FleetHeatmap'
import { Histogram, HistogramTable } from '../charts/Histogram'
import { StatusBars, StatusBarsTable, StatusLegend } from '../charts/StatusBars'
import { useFormat } from '../i18n/format'
import { dailyQualityQuery, deviceHealthQuery } from '../queries'
import { cn } from '../ui/cn'
import { Empty } from '../ui/Empty'
import { Hint } from '../ui/Hint'
import { Pagination } from '../ui/Pagination'
import { TractionIcon } from '../ui/icons'
import { QueryState } from '../ui/QueryState'
import { SegmentedItem, SegmentedRoot } from '../ui/Segmented'
import { Select } from '../ui/Select'
import { STATUS_BG } from '../ui/status'
import { StatusPill } from '../ui/StatusPill'
import { NUM, TABLE, TD, TH, WRAP } from '../ui/table'
import { ActiveFilters } from './ActiveFilters'
import { matches, NO_FILTERS, type FilterKey, type Filters } from './filters'
import { METRIC_ORDER, METRICS, type MetricId } from './metrics'

type Format = ReturnType<typeof useFormat>

const STATUS_ORDER: HealthStatus[] = ['Fault', 'Warning', 'Ok', 'Unknown']

// Only the features these tables use are registered (TanStack Table v9 is opt-in per feature).
// Filtering happens before the table (quality/filters.ts), because the charts need the same
// filters applied with one dimension left out — the table sorts, pages and expands.
const vehicleFeatures = tableFeatures({
  rowSortingFeature,
  sortedRowModel: createSortedRowModel(),
  rowExpandingFeature,
  expandedRowModel: createExpandedRowModel(),
  rowPaginationFeature,
  paginatedRowModel: createPaginatedRowModel(),
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
  if (reasons.length === 0) return <span className="text-ink-2">—</span>
  return (
    <ul className="space-y-0.5">
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

/** A share cell coloured by the same thresholds the backend used. */
function Share({ value, warning, fault, format }: { value: number | undefined; warning: number; fault?: number; format: Format }) {
  if (value === undefined) return <span className="text-ink-2">—</span>
  const level = fault !== undefined && value >= fault ? 'fault' : value >= warning ? 'warning' : ''
  return (
    <span
      className={cn(
        'inline-block min-w-[58px] rounded-[5px] px-[7px] py-px',
        level === 'warning' && 'bg-warning-soft font-semibold text-warning',
        level === 'fault' && 'bg-fault-soft font-semibold text-fault',
      )}
    >
      {format.percent(value)}
    </span>
  )
}

function HeaderHint({ label, hint }: { label: string; hint: string }) {
  return (
    <Hint text={hint}>
      <span className="cursor-help border-b border-dotted border-current">{label}</span>
    </Hint>
  )
}

function buildVehicleColumns(t: TFunction, th: HealthThresholds, format: Format) {
  return vehicleColumns.columns([
    vehicleColumns.accessor('status', {
      header: t('health.columns.status'),
      cell: (info) => <StatusPill status={info.getValue()} />,
      sortFn: byStatus,
    }),
    vehicleColumns.accessor('vehicleId', { header: t('health.columns.vehicle'), cell: (info) => <span className="font-display text-lg leading-[1.1] font-bold">{info.getValue()}</span> }),
    vehicleColumns.accessor((v) => v.traction ?? '', {
      id: 'traction',
      header: t('health.columns.traction'),
      cell: (info) => (
        <span className="inline-flex items-center gap-1.5 [&>svg]:shrink-0 [&>svg]:text-ink-2">
          <TractionIcon traction={info.getValue()} />
          <span>
            {tractionLabel(t, info.getValue())} <span className="text-sm text-ink-2">{info.row.original.model ?? ''}</span>
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
  return <span className="text-route">{sorted === 'asc' ? ' ▴' : sorted === 'desc' ? ' ▾' : ''}</span>
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
    <table className={cn(TABLE, 'rounded-lg border border-rule bg-paper text-sm')}>
      <thead>
        {table.getHeaderGroups().map((group) => (
          <tr key={group.id}>
            {group.headers.map((header) => (
              <th key={header.id} className={cn(TH, DEVICE_NUMERIC.has(header.column.id) && NUM)}>
                {header.column.getCanSort() ? (
                  <button className="inline-flex cursor-pointer items-center gap-0.5 hover:text-ink" onClick={header.column.getToggleSortingHandler()}>
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
              <td key={cell.id} className={cn(TD, DEVICE_NUMERIC.has(cell.column.id) && NUM, cell.column.id === 'reasons' && WRAP)}>
                <table.FlexRender cell={cell} />
              </td>
            ))}
          </tr>
        ))}
      </tbody>
    </table>
  )
}

/**
 * How status is decided, in plain language — opened from the page header, where the
 * question arises, as a side panel the reader can keep open next to the table.
 */
/** One rule in the side panel: heading, then short paragraphs. */
const RULE = 'border-t border-rule py-3.5 [&_h3]:mb-1.5 [&_h3]:text-lg [&_p]:mb-2 [&_p]:leading-[1.55] [&_p:last-child]:mb-0'

function RulesPanel({ th }: { th: HealthThresholds }) {
  const { t } = useTranslation()
  const format = useFormat()
  const markup = { strong: <strong />, code: <code />, p: <p /> }
  const rules = [
    { key: 'negative', values: { warning: format.percent(th.negativeOccupancyWarning), fault: format.percent(th.negativeOccupancyFault) } },
    { key: 'silent', values: {} },
    { key: 'imbalance', values: { min: th.minPassengersForBalance, warning: format.percent(th.imbalanceWarning), fault: format.percent(th.imbalanceFault) } },
    { key: 'flagged', values: { warning: format.percent(th.flaggedStopsWarning) } },
  ] as const

  return (
    <Dialog.Root>
      <Dialog.Trigger className="inline-flex cursor-pointer items-center gap-1 align-bottom font-semibold text-route hover:text-route-strong hover:underline">
        <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden="true">
          <circle cx="12" cy="12" r="9" />
          <path d="M12 11v5M12 8h.01" />
        </svg>
        {t('health.rules.open')}
      </Dialog.Trigger>
      <Dialog.Portal>
        <Dialog.Overlay className="fixed inset-0 z-[2000] bg-[rgb(17_26_31/0.28)]" />
        <Dialog.Content className="fixed inset-y-0 right-0 z-[2001] w-[min(460px,100vw)] overflow-y-auto border-l border-rule bg-paper px-6 pt-5 pb-8 shadow-float data-[state=open]:animate-sheet-in">
          <div className="flex items-center justify-between gap-3">
            <Dialog.Title className="text-xl">{t('health.rules.title')}</Dialog.Title>
            <Dialog.Close className="inline-flex cursor-pointer rounded-lg p-1.5 text-ink-2 hover:bg-surface hover:text-ink" aria-label={t('health.rules.close')}>
              <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden="true">
                <path d="M6 6l12 12M18 6 6 18" />
              </svg>
            </Dialog.Close>
          </div>
          <Dialog.Description className="mt-2 mb-[18px] text-ink-2">{t('health.rules.lead')}</Dialog.Description>
          {rules.map((rule) => (
            <section key={rule.key} className={RULE}>
              <h3>{t(`health.rules.${rule.key}.title`)}</h3>
              <Trans i18nKey={`health.rules.${rule.key}.body`} values={rule.values} components={markup} />
            </section>
          ))}
          <section className={RULE}>
            <h3>{t('health.rules.notes.title')}</h3>
            <ul className="list-disc space-y-1.5 pl-[18px] text-ink-2">
              <li>{t('health.rules.notes.counts')}</li>
              <li>{t('health.rules.notes.restarts')}</li>
              <li>{t('health.rules.notes.provisional')}</li>
            </ul>
          </section>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  )
}

function VehicleHealthTable({ report, daily }: { report: DeviceHealthReport; daily: VehicleDay[] }) {
  const { t, i18n } = useTranslation()
  const format = useFormat()
  const [filters, setFilters] = useState<Filters>(NO_FILTERS)
  const [metricId, setMetricId] = useState<MetricId>('negative')
  const [groupBy, setGroupBy] = useState<'model' | 'firmware'>('model')
  const tableTop = useRef<HTMLDivElement>(null)
  const metric = METRICS[metricId]

  const text = useMemo(
    () => (v: VehicleHealth) => searchText(t, v),
    // eslint-disable-next-line react-hooks/exhaustive-deps -- depends on the language only
    [i18n.resolvedLanguage],
  )
  const set = (patch: Partial<Filters>) => setFilters((f) => ({ ...f, ...patch }))
  /** Vehicles passing every filter except the given ones — what a chart shows for its own dimension. */
  const except = (...keys: FilterKey[]) => report.vehicles.filter((v) => matches(v, filters, text, keys))
  const tableData = useMemo(() => report.vehicles.filter((v) => matches(v, filters, text)), [report.vehicles, filters, text])
  const columns = useMemo(
    () => buildVehicleColumns(t, report.thresholds, format),
    // eslint-disable-next-line react-hooks/exhaustive-deps -- labels change with the language
    [report.thresholds, i18n.resolvedLanguage],
  )

  const table = useTable({
    features: vehicleFeatures,
    columns,
    data: tableData,
    getRowId: (v) => String(v.vehicleId),
    getRowCanExpand: () => true,
    initialState: { sorting: [{ id: 'status', desc: false }], pagination: { pageIndex: 0, pageSize: 25 } },
    // A new filter result or sort sends the reader back to page 1 (TanStack's default reset).
  })

  const all = report.vehicles
  // Like a Power BI slicer, the status counts react to every filter except status itself.
  const forStatus = except('status')
  const counts = Object.fromEntries(STATUS_ORDER.map((s) => [s, forStatus.filter((v) => v.status === s).length])) as Record<HealthStatus, number>
  const tractions = [...new Set(all.map((v) => v.traction).filter((x): x is string => x !== null))].sort()
  const deviceTotal = all.reduce((n, v) => n + v.devices.length, 0)

  const statusFilter = filters.status ?? ''
  const tractionFilter = filters.traction ?? 'all'
  // The table body shows one page; everything else — charts, counts — sees every filtered,
  // sorted row, so paging never changes what the analysis says.
  const shown = table.getRowModel().rows
  const filtered = table.getPrePaginatedRowModel().rows
  const visible = filtered.map((row) => row.original)
  const { pageIndex, pageSize } = table.state.pagination
  const dailyByKey = useMemo(() => new Map(daily.map((d) => [`${d.vehicleId}|${d.day}`, d])), [daily])
  const days = useMemo(() => heatmapDays(report.from, report.to), [report.from, report.to])
  const formatMetric = (value: number) => (metric.kind === 'share' ? format.percentWhole(value) : format.number(Math.round(value)))
  const metricName = t(`charts.metrics.${metricId}`)
  const thresholds = metric.thresholds?.(report.thresholds)
  const histogramBins = binValues(
    except('range')
      .map(metric.vehicle)
      .filter((v): v is number => v !== null),
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
    // The bars keep showing every group (the selected one highlighted), so they ignore their own filters.
    const source = groupBy === 'model' ? except('model', 'status') : except('firmware')
    for (const v of source) {
      if (groupBy === 'model') add(v.model ?? '', v.model ?? t('charts.unknownModel'), v.status)
      else for (const d of v.devices) add(d.firmwareVersion ?? '', d.firmwareVersion ?? t('charts.unknownFirmware'), d.status)
    }
    return [...groups.values()]
  })()
  const toggleRange = (bin: { from: number; to: number }, isLast: boolean) =>
    setFilters((f) =>
      f.range?.metric === metricId && f.range.from === bin.from
        ? { ...f, range: undefined }
        : { ...f, range: { metric: metricId, from: bin.from, to: isLast ? Infinity : bin.to } },
    )
  const toggleGroup = (key: string) =>
    setFilters((f) =>
      groupBy === 'model'
        ? { ...f, model: f.model === key ? undefined : key }
        : { ...f, firmware: f.firmware === key ? undefined : key },
    )
  const toggleSegment = (key: string, status: HealthStatus) =>
    setFilters((f) =>
      groupBy === 'firmware'
        ? { ...f, firmware: f.firmware === key ? undefined : key }
        : f.model === key && f.status === status
          ? { ...f, model: undefined, status: undefined }
          : { ...f, model: key, status },
    )

  return (
    <div className="flex-1 overflow-y-auto p-4 md:px-7 md:pt-6 md:pb-10">
      <header>
        <h2 className="mb-2 text-2xl">{t('health.title')}</h2>
        <dl className="flex flex-wrap gap-x-7 gap-y-1.5 [&>div]:flex [&>div]:items-baseline [&>div]:gap-2 [&_dd]:font-semibold [&_dt]:text-ink-2">
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
        <p className="mt-1.5 max-w-[72ch] text-sm text-ink-2">
          {t('health.note')} <RulesPanel th={report.thresholds} />
        </p>
      </header>

      <section className="mt-[22px] mb-[18px]" aria-label={t('health.fleetStatus')}>
        {/* Decorative summary of the counts below; the toggle buttons carry the same numbers as text. */}
        <div className="mb-2.5 flex h-3.5 gap-[3px]" aria-hidden="true">
          {STATUS_ORDER.filter((s) => counts[s] > 0).map((s) => (
            <span
              key={s}
              className={cn('min-w-1 rounded-[3px] transition-opacity', STATUS_BG[s], statusFilter && statusFilter !== s && 'opacity-20')}
              style={{ flexGrow: counts[s] }}
            />
          ))}
        </div>
        <ToggleGroup.Root
          type="single"
          className="flex flex-wrap gap-2"
          aria-label={t('health.statusFilter')}
          value={statusFilter}
          onValueChange={(value) => set({ status: (value || undefined) as HealthStatus | undefined })}
        >
          {STATUS_ORDER.map((s) => (
            <ToggleGroup.Item
              key={s}
              value={s}
              className="inline-flex cursor-pointer items-center gap-2 rounded-full border border-rule bg-paper py-[5px] pr-3 pl-2 hover:bg-surface data-[state=on]:border-ink data-[state=on]:bg-surface-2"
            >
              <StatusPill status={s} />
              <span className="font-display text-lg leading-none font-bold">{format.number(counts[s])}</span>
            </ToggleGroup.Item>
          ))}
        </ToggleGroup.Root>
      </section>

      <div className="mb-3 flex flex-wrap items-center gap-4" role="search">
        <label className="inline-flex h-8 items-center gap-1.5 rounded-lg border border-rule bg-paper px-2.5 text-ink-2 focus-within:border-route focus-within:shadow-[0_0_0_1px_var(--route)]">
          <span className="sr-only">{t('health.searchLabel')}</span>
          <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden="true">
            <circle cx="11" cy="11" r="7" />
            <path d="m20 20-3.5-3.5" />
          </svg>
          <input
            className="w-[150px] bg-transparent text-ink outline-none md:w-[220px]"
            type="search"
            value={filters.search} placeholder={t('health.searchPlaceholder')} onChange={(event) => set({ search: event.target.value })} />
        </label>
        <Select
          label={t('health.traction')}
          value={tractionFilter}
          options={[{ value: 'all', label: t('health.allTractions') }, ...tractions.map((x) => ({ value: x, label: tractionLabel(t, x) }))]}
          onChange={(value) => set({ traction: value === 'all' ? undefined : value })}
        />
        <Select
          label={t('charts.metric')}
          value={metricId}
          options={METRIC_ORDER.map((id) => ({ value: id, label: t(`charts.metrics.${id}`) }))}
          onChange={(value) => setMetricId(value as MetricId)}
        />
      </div>

      <ActiveFilters
        filters={filters}
        onChange={setFilters}
        tractionLabel={tractionLabel}
        formatRange={(id, value) => (METRICS[id].kind === 'share' ? format.percentWhole(value) : format.number(Math.round(value)))}
      />

      <section className="mb-7 flex flex-col gap-4" aria-label={t('charts.analysis')}>
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
              onSelectVehicle={(id) => set({ search: String(id) })}
            />
          }
          table={<HeatmapTable vehicles={visible} days={days} daily={dailyByKey} metric={metric} formatValue={formatMetric} />}
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
                selectedFrom={filters.range?.metric === metricId ? filters.range.from : undefined}
                onSelect={toggleRange}
              />
            }
            table={<HistogramTable bins={histogramBins} formatValue={formatMetric} countHeader={t('charts.vehiclesHeader')} />}
          />
          <ChartFigure
            title={t(groupBy === 'model' ? 'charts.statusByModel' : 'charts.statusByFirmware')}
            subtitle={t(groupBy === 'model' ? 'charts.statusByModelSubtitle' : 'charts.statusByFirmwareSubtitle')}
            controls={
              <SegmentedRoot
                type="single"
                value={groupBy}
                aria-label={t('charts.groupBy')}
                onValueChange={(value) => value && setGroupBy(value as 'model' | 'firmware')}
              >
                <SegmentedItem value="model">
                  {t('charts.byModel')}
                </SegmentedItem>
                <SegmentedItem value="firmware">
                  {t('charts.byFirmware')}
                </SegmentedItem>
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

      <div className="mb-2.5 flex items-baseline gap-3">
        <h3 className="text-xl">{t('health.vehiclesTitle')}</h3>
        <span className="text-xs text-ink-2">{t('health.shown', { count: filtered.length })}</span>
      </div>

      <div className="overflow-x-auto rounded-[10px] border border-rule" ref={tableTop}>
        <table className={TABLE}>
          <thead>
            {table.getHeaderGroups().map((group) => (
              <tr key={group.id}>
                {group.headers.map((header) => (
                  <th
                    key={header.id}
                    className={cn(TH, NUMERIC.has(header.column.id) && NUM)}
                    aria-sort={header.column.getIsSorted() === 'asc' ? 'ascending' : header.column.getIsSorted() === 'desc' ? 'descending' : 'none'}
                  >
                    {header.column.getCanSort() ? (
                      <button className="inline-flex cursor-pointer items-center gap-0.5 hover:text-ink" onClick={header.column.getToggleSortingHandler()}>
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
                <tr
                  className="cursor-pointer hover:[&>td]:bg-surface aria-expanded:[&>td]:bg-route-soft"
                  onClick={() => row.toggleExpanded()} aria-expanded={row.getIsExpanded()}>
                  {row.getAllCells().map((cell) => (
                    <td key={cell.id} className={cn(TD, NUMERIC.has(cell.column.id) && NUM, cell.column.id === 'reasons' && WRAP)}>
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

      <Pagination
        pageIndex={pageIndex}
        pageSize={pageSize}
        rowCount={filtered.length}
        onPageChange={(page) => {
          table.setPageIndex(page)
          tableTop.current?.scrollIntoView({ block: 'nearest' })
        }}
        onPageSizeChange={(size) => table.setPageSize(size)}
      />

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
          <Empty>{t('health.empty')}</Empty>
        ) : (
          // The heatmap fills in when the per-day data arrives; the rest doesn't wait for it.
          <VehicleHealthTable report={data} daily={daily.data ?? EMPTY_DAYS} />
        )
      }
    </QueryState>
  )
}
