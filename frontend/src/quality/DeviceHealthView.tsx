import { useQuery } from '@tanstack/react-query'
import {
  columnFilteringFeature,
  createColumnHelper,
  createExpandedRowModel,
  createFilteredRowModel,
  createSortedRowModel,
  filterFn_equals,
  functionalUpdate,
  rowExpandingFeature,
  rowSortingFeature,
  tableFeatures,
  useTable,
  type ColumnFiltersState,
  type SortFn,
} from '@tanstack/react-table'
import type { TFunction } from 'i18next'
import { Collapsible, ToggleGroup } from 'radix-ui'
import { Fragment, useMemo, useState } from 'react'
import { Trans, useTranslation } from 'react-i18next'
import type { DeviceHealth, DeviceHealthReport, HealthReason, HealthStatus, HealthThresholds, VehicleHealth } from '../api'
import { useFormat } from '../i18n/format'
import { deviceHealthQuery } from '../queries'
import { Hint } from '../ui/Hint'
import { StatusIcon, TractionIcon } from '../ui/icons'
import { QueryState } from '../ui/QueryState'
import { Select } from '../ui/Select'

type Format = ReturnType<typeof useFormat>

const STATUS_ORDER: HealthStatus[] = ['Fault', 'Warning', 'Ok', 'Unknown']

// Only the features these tables use are registered (TanStack Table v9 is opt-in per feature).
const vehicleFeatures = tableFeatures({
  rowSortingFeature,
  sortedRowModel: createSortedRowModel(),
  columnFilteringFeature,
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

function VehicleHealthTable({ report }: { report: DeviceHealthReport }) {
  const { t, i18n } = useTranslation()
  const format = useFormat()
  const [columnFilters, setColumnFilters] = useState<ColumnFiltersState>([])
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
    state: { columnFilters },
    onColumnFiltersChange: (updater) => setColumnFilters((previous) => functionalUpdate(updater, previous)),
  })

  const all = report.vehicles
  const counts = Object.fromEntries(STATUS_ORDER.map((s) => [s, all.filter((v) => v.status === s).length])) as Record<HealthStatus, number>
  const tractions = [...new Set(all.map((v) => v.traction).filter((x): x is string => x !== null))].sort()
  const deviceTotal = all.reduce((n, v) => n + v.devices.length, 0)

  const statusFilter = (columnFilters.find((f) => f.id === 'status')?.value as HealthStatus | undefined) ?? ''
  const tractionFilter = (columnFilters.find((f) => f.id === 'traction')?.value as string | undefined) ?? 'all'
  const shown = table.getRowModel().rows

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

      <div className="filters">
        <Select
          label={t('health.traction')}
          value={tractionFilter}
          options={[{ value: 'all', label: t('health.allTractions') }, ...tractions.map((x) => ({ value: x, label: tractionLabel(t, x) }))]}
          onChange={(value) => table.getColumn('traction')?.setFilterValue(value === 'all' ? undefined : value)}
        />
        {columnFilters.length > 0 && (
          <button className="link-button" onClick={() => setColumnFilters([])}>
            {t('health.clearFilters')}
          </button>
        )}
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
  return (
    <QueryState query={report} loading={t('health.loading')}>
      {(data) => (data.vehicles.length === 0 ? <p className="empty">{t('health.empty')}</p> : <VehicleHealthTable report={data} />)}
    </QueryState>
  )
}
