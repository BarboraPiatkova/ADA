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
import { Collapsible, ToggleGroup } from 'radix-ui'
import { Fragment, useMemo, useState } from 'react'
import type { DeviceHealth, DeviceHealthReport, HealthStatus, HealthThresholds, VehicleHealth } from '../api'
import { deviceHealthQuery } from '../queries'
import { Hint } from '../ui/Hint'
import { QueryState } from '../ui/QueryState'
import { Select } from '../ui/Select'

const nf = new Intl.NumberFormat('cs-CZ')
const pf = new Intl.NumberFormat('cs-CZ', { style: 'percent', maximumFractionDigits: 1 })

const STATUS_LABEL: Record<HealthStatus, string> = {
  Fault: 'Porucha',
  Warning: 'Varování',
  Ok: 'V pořádku',
  Unknown: 'Málo dat',
}
const STATUS_ORDER: HealthStatus[] = ['Fault', 'Warning', 'Ok', 'Unknown']
const EMPTY_VEHICLES: VehicleHealth[] = []

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

function StatusPill({ status }: { status: HealthStatus }) {
  return <span className={`pill pill-${status.toLowerCase()}`}>{STATUS_LABEL[status]}</span>
}

/** A share cell coloured by the same thresholds the backend used. */
function Share({ value, warning, fault }: { value: number | undefined; warning: number; fault?: number }) {
  if (value === undefined) return <span className="muted">—</span>
  const level = fault !== undefined && value >= fault ? 'fault' : value >= warning ? 'warning' : ''
  return <span className={level ? `share share-${level}` : 'share'}>{pf.format(value)}</span>
}

function HeaderHint({ label, hint }: { label: string; hint: string }) {
  return (
    <Hint text={hint}>
      <span className="has-hint">{label}</span>
    </Hint>
  )
}

function buildVehicleColumns(t: HealthThresholds) {
  return vehicleColumns.columns([
    vehicleColumns.accessor('status', {
      header: 'Stav',
      cell: (info) => <StatusPill status={info.getValue()} />,
      sortFn: byStatus,
      filterFn: 'equals',
    }),
    vehicleColumns.accessor('vehicleId', { header: 'Vůz', cell: (info) => <strong>{info.getValue()}</strong> }),
    vehicleColumns.accessor((v) => v.traction ?? '', {
      id: 'traction',
      header: 'Trakce',
      filterFn: 'equals',
      cell: (info) => (
        <>
          {info.getValue() || '—'} <span className="muted small">{info.row.original.model ?? ''}</span>
        </>
      ),
    }),
    vehicleColumns.accessor('boardings', { header: 'Nástupy', cell: (info) => nf.format(info.getValue()) }),
    vehicleColumns.accessor('alightings', { header: 'Výstupy', cell: (info) => nf.format(info.getValue()) }),
    vehicleColumns.accessor((v) => v.imbalance ?? undefined, {
      id: 'imbalance',
      header: () => <HeaderHint label="Nesoulad" hint="|nástupy − výstupy| / (nástupy + výstupy) za celé období" />,
      sortUndefined: 'last',
      cell: (info) => <Share value={info.getValue()} warning={t.imbalanceWarning} fault={t.imbalanceFault} />,
    }),
    vehicleColumns.accessor((v) => v.negativeOccupancyShare ?? undefined, {
      id: 'negative',
      header: () => <HeaderHint label="Záporná obsaz." hint="Podíl zastavení, po kterých vůz vede méně než nula cestujících" />,
      sortUndefined: 'last',
      cell: (info) => <Share value={info.getValue()} warning={t.negativeOccupancyWarning} fault={t.negativeOccupancyFault} />,
    }),
    vehicleColumns.accessor((v) => v.flaggedStopShare ?? undefined, {
      id: 'flagged',
      header: () => <HeaderHint label="Příznak chyby" hint="Podíl zastavení, kdy vůz označil některou jednotku jako chybnou (chyba)" />,
      sortUndefined: 'last',
      cell: (info) => <Share value={info.getValue()} warning={t.flaggedStopsWarning} />,
    }),
    vehicleColumns.display({
      id: 'reasons',
      header: 'Důvod',
      cell: (info) => info.row.original.reasons.join(' · ') || <span className="muted">—</span>,
    }),
  ])
}

const NUMERIC = new Set(['vehicleId', 'boardings', 'alightings', 'imbalance', 'negative', 'flagged'])

const deviceColumnDefs = deviceColumns.columns([
  deviceColumns.accessor('status', { header: 'Stav', cell: (info) => <StatusPill status={info.getValue()} />, sortFn: byStatus }),
  deviceColumns.accessor('deviceNumber', { header: 'Jednotka', cell: (info) => <strong>{info.getValue()}</strong> }),
  deviceColumns.accessor((d) => d.firmwareVersion ?? '', { id: 'firmware', header: 'Firmware', cell: (info) => info.getValue() || '—' }),
  deviceColumns.accessor('stopsCounted', { header: 'Zastavení', cell: (info) => nf.format(info.getValue()) }),
  deviceColumns.accessor('boardings', { header: 'Nástupy', cell: (info) => nf.format(info.getValue()) }),
  deviceColumns.accessor('alightings', { header: 'Výstupy', cell: (info) => nf.format(info.getValue()) }),
  deviceColumns.accessor('notAliveHeartbeats', {
    header: 'alive=false',
    cell: (info) =>
      info.row.original.heartbeats === 0 ? '—' : `${nf.format(info.getValue())} / ${nf.format(info.row.original.heartbeats)}`,
  }),
  deviceColumns.accessor('restarts', { header: 'Restarty', cell: (info) => nf.format(info.getValue()) }),
  deviceColumns.accessor('flaggedStops', { header: 'Příznak chyby', cell: (info) => nf.format(info.getValue()) }),
  deviceColumns.display({
    id: 'reasons',
    header: 'Důvod',
    cell: (info) => info.row.original.reasons.join(' · ') || <span className="muted">—</span>,
  }),
])
const DEVICE_NUMERIC = new Set(['deviceNumber', 'stopsCounted', 'boardings', 'alightings', 'notAliveHeartbeats', 'restarts', 'flaggedStops'])

function SortIndicator({ sorted }: { sorted: false | 'asc' | 'desc' }) {
  return <span className="sort-indicator">{sorted === 'asc' ? ' ▴' : sorted === 'desc' ? ' ▾' : ''}</span>
}

function DeviceTable({ devices }: { devices: DeviceHealth[] }) {
  const table = useTable({
    features: deviceFeatures,
    columns: deviceColumnDefs,
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

function Rules({ t }: { t: HealthThresholds }) {
  return (
    <Collapsible.Root className="rules">
      <Collapsible.Trigger className="link-button">Pravidla vyhodnocení (předběžná) ▾</Collapsible.Trigger>
      <Collapsible.Content>
        <ul>
          <li>
            <strong>Nesoulad</strong> = |nástupy − výstupy| / (nástupy + výstupy) za celé období, jen při alespoň {t.minPassengersForBalance}{' '}
            cestujících. Varování od {pf.format(t.imbalanceWarning)}, porucha od {pf.format(t.imbalanceFault)}.
          </li>
          <li>
            <strong>Záporná obsazenost</strong> = podíl zastavení, po kterých palubní počítač vede ve voze méně než nula cestujících. Varování od{' '}
            {pf.format(t.negativeOccupancyWarning)}, porucha od {pf.format(t.negativeOccupancyFault)}.
          </li>
          <li>
            <strong>Příznak chyby</strong> = podíl zastavení, kdy vůz označil některou jednotku jako chybnou (<code>chyba</code>). Varování od{' '}
            {pf.format(t.flaggedStopsWarning)}.
          </li>
          <li>
            <strong>Mlčící jednotka</strong> = dokončovala sčítání, ale za celé období nenapočítala nikoho — porucha, pokud mlčí všechny jednotky vozu.
          </li>
          <li>
            Počty na zastávce = rozdíl stavu čítače mezi zahájením a ukončením sčítání (hodnoty v logu jsou průběžné stavy, viz report F11).
          </li>
          <li>Restarty se nehodnotí: v datech jsou běžnou provozní událostí (viz report F5).</li>
        </ul>
      </Collapsible.Content>
    </Collapsible.Root>
  )
}

function VehicleHealthTable({ report }: { report: DeviceHealthReport }) {
  const [columnFilters, setColumnFilters] = useState<ColumnFiltersState>([])
  const columns = useMemo(() => buildVehicleColumns(report.thresholds), [report.thresholds])

  const table = useTable({
    features: vehicleFeatures,
    columns,
    data: report.vehicles ?? EMPTY_VEHICLES,
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
        <h2>Stav sčítacích jednotek</h2>
        <p className="muted">
          {report.from} – {report.to} · {nf.format(all.length)} vozidel · {nf.format(deviceTotal)} jednotek. Vyhodnoceno ze surových zpráv
          podle předběžných pravidel (níže).
        </p>
      </header>

      <ToggleGroup.Root
        type="single"
        className="tiles"
        aria-label="Filtr stavu"
        value={statusFilter}
        onValueChange={(value) => table.getColumn('status')?.setFilterValue(value || undefined)}
      >
        {STATUS_ORDER.map((s) => (
          <ToggleGroup.Item key={s} value={s} className={`tile tile-${s.toLowerCase()}`}>
            <span className="tile-value">{counts[s]}</span>
            <span className="tile-label">{STATUS_LABEL[s]}</span>
          </ToggleGroup.Item>
        ))}
      </ToggleGroup.Root>

      <div className="filters">
        <Select
          label="Trakce"
          value={tractionFilter}
          options={[{ value: 'all', label: 'všechny' }, ...tractions.map((x) => ({ value: x, label: x }))]}
          onChange={(value) => table.getColumn('traction')?.setFilterValue(value === 'all' ? undefined : value)}
        />
        {columnFilters.length > 0 && (
          <button className="link-button" onClick={() => setColumnFilters([])}>
            zrušit filtry
          </button>
        )}
        <span className="muted small">{shown.length} zobrazeno · kliknutím na řádek zobrazíte jednotky</span>
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

      <Rules t={report.thresholds} />
    </div>
  )
}

export function DeviceHealthView() {
  const report = useQuery(deviceHealthQuery)
  return (
    <QueryState query={report} loading="Počítám stav jednotek ze surových dat…">
      {(data) =>
        data.vehicles.length === 0 ? <p className="empty">Zatím nejsou importované žádné surové logy.</p> : <VehicleHealthTable report={data} />
      }
    </QueryState>
  )
}
