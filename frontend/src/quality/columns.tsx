/* oxlint-disable react/only-export-components -- column definitions are data whose cell renderers return JSX; the file exports no components (those are in cells.tsx). Editing it reloads the page instead of hot-swapping, which is fine. */
import {
  createColumnHelper,
  createExpandedRowModel,
  createPaginatedRowModel,
  createSortedRowModel,
  rowExpandingFeature,
  rowPaginationFeature,
  rowSortingFeature,
  tableFeatures,
  type SortFn,
} from '@tanstack/react-table'
import type { TFunction } from 'i18next'
import type { DeviceHealth, HealthStatus, HealthThresholds, VehicleHealth } from '../api'
import type { Format } from '../i18n/format'
import { TractionIcon } from '../ui/icons'
import { STATUS_ORDER } from '../ui/status'
import { StatusPill } from '../ui/StatusPill'
import { HeaderHint, Share } from './cells'
import { tractionLabel } from './labels'
import { Reasons } from './Reasons'

// Only the features these tables use are registered (TanStack Table v9 is opt-in per feature).
// Filtering happens before the table (quality/filters.ts), because the charts need the same
// filters applied with one dimension left out — the table sorts, pages and expands.
export const vehicleFeatures = tableFeatures({
  rowSortingFeature,
  sortedRowModel: createSortedRowModel(),
  rowExpandingFeature,
  expandedRowModel: createExpandedRowModel(),
  rowPaginationFeature,
  paginatedRowModel: createPaginatedRowModel(),
})
export const deviceFeatures = tableFeatures({ rowSortingFeature, sortedRowModel: createSortedRowModel() })

const vehicleColumns = createColumnHelper<typeof vehicleFeatures, VehicleHealth>()
const deviceColumns = createColumnHelper<typeof deviceFeatures, DeviceHealth>()

/** Columns whose values are numbers: right-aligned so digits line up. */
export const VEHICLE_NUMERIC = new Set(['vehicleId', 'boardings', 'alightings', 'imbalance', 'negative', 'flagged'])
export const DEVICE_NUMERIC = new Set(['deviceNumber', 'stopsCounted', 'boardings', 'alightings', 'notAliveHeartbeats', 'restarts', 'flaggedStops'])

/** Worst first when sorted ascending. */
const byStatus: SortFn<any, any> = (a, b, id) =>
  STATUS_ORDER.indexOf(a.getValue<HealthStatus>(id)) - STATUS_ORDER.indexOf(b.getValue<HealthStatus>(id))

export function buildVehicleColumns(t: TFunction, th: HealthThresholds, format: Format) {
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
            {info.getValue() ? tractionLabel(t, info.getValue()) : '—'} <span className="text-sm text-ink-2">{info.row.original.model ?? ''}</span>
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
    vehicleColumns.display({ id: 'reasons', header: t('health.columns.reasons'), cell: (info) => <Reasons reasons={info.row.original.reasons} /> }),
  ])
}

export function buildDeviceColumns(t: TFunction, format: Format) {
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
    deviceColumns.display({ id: 'reasons', header: t('health.columns.reasons'), cell: (info) => <Reasons reasons={info.row.original.reasons} /> }),
  ])
}
