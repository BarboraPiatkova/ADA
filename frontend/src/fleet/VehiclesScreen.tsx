import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { createColumnHelper } from '@tanstack/react-table'
import type { TFunction } from 'i18next'
import { useEffect, useMemo, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { FleetReport, FleetVehicle, Period, VehicleDaySummary, VehicleDevice, VehicleFault } from '../api'
import { useFormat, type Format } from '../i18n/format'
import { numberParam, useScreenParams } from '../navigation'
import { FILTER_BAR, usePeriod, useReportPeriod } from '../operations/period'
import { matchesRow } from '../operations/shared'
import { SortableTable } from '../operations/SortableTable'
import { sortableFeatures } from '../operations/tableFeatures'
import { DayKindSelect } from '../operations/TimeView'
import { VehicleDay } from '../operations/VehicleDay'
import { HealthSkeleton } from '../quality/HealthSkeleton'
import { tractionLabel } from '../quality/labels'
import { fleetQuery, vehicleDetailQuery } from '../queries'
import { downloadCsv, type CsvColumn } from '../ui/csv'
import { DateRangePicker } from '../ui/DateRangePicker'
import { Empty } from '../ui/Empty'
import { LinkButton } from '../ui/LinkButton'
import { QueryState } from '../ui/QueryState'
import { SearchInput } from '../ui/SearchInput'

const BUTTON = 'inline-flex h-8 cursor-pointer items-center touch-target rounded-lg border border-rule bg-paper px-3 text-sm text-ink hover:border-ink-2'
const CLOSE =
  'inline-flex size-8 shrink-0 cursor-pointer items-center justify-center touch-target rounded-lg border border-rule bg-paper text-ink-2 hover:border-ink-2 hover:text-ink'

/**
 * The fleet register, as ADA's "Vozidla" listed it: every vehicle with its details and what its logs
 * hold for the period; a vehicle opens its devices, days with trips and known faults, and a day its trips.
 * "#/vozidla?vehicle=1083" opens on that vehicle.
 */
export function VehiclesScreen() {
  const { t } = useTranslation()
  const params = useScreenParams()
  const [selected, setSelected] = useState<number | null>(() => numberParam(params, 'vehicle'))
  const { period } = useReportPeriod()
  const report = useQuery({ ...fleetQuery(period), placeholderData: keepPreviousData })
  return (
    <QueryState query={report} loading={t('fleet.loading')} skeleton={<HealthSkeleton label={t('fleet.loading')} />}>
      {(data) =>
        data.vehicles.length === 0 ? <Empty>{t('fleet.empty')}</Empty> : <VehiclesView report={data} period={period} selected={selected} onSelect={setSelected} />
      }
    </QueryState>
  )
}

function VehiclesView({
  report,
  period,
  selected,
  onSelect,
}: {
  report: FleetReport
  period: Period
  selected: number | null
  onSelect: (vehicle: number | null) => void
}) {
  const { t } = useTranslation()
  const format = useFormat()
  const [range, setRange] = usePeriod()
  const panel = useRef<HTMLElement>(null)

  useEffect(() => {
    if (selected === null) return
    panel.current?.scrollIntoView({ behavior: 'smooth', block: 'start' })
    panel.current?.focus({ preventScroll: true })
  }, [selected])

  const trips = report.vehicles.reduce((sum, v) => sum + v.trips, 0)
  const invalid = report.vehicles.reduce((sum, v) => sum + v.invalidTrips, 0)
  return (
    <div className="flex-1 overflow-y-auto p-4 md:px-7 md:pt-6 md:pb-10">
      <header>
        <h1 className="mb-2 text-2xl">{t('fleet.title')}</h1>
        <dl className="flex flex-wrap gap-x-7 gap-y-1.5 [&>div]:flex [&>div]:items-baseline [&>div]:gap-2 [&_dd]:font-semibold [&_dt]:text-ink-2">
          <div>
            <dt>{t('fleet.facts.period')}</dt>
            <dd>
              {report.from ? format.date(report.from) : '?'} – {report.to ? format.date(report.to) : '?'}
            </dd>
          </div>
          <div>
            <dt>{t('fleet.facts.vehicles')}</dt>
            <dd>{format.number(report.vehicles.length)}</dd>
          </div>
          <div>
            <dt>{t('fleet.facts.withTrips')}</dt>
            <dd>{format.number(report.vehicles.filter((v) => v.trips > 0).length)}</dd>
          </div>
          <div>
            <dt>{t('fleet.facts.trips')}</dt>
            <dd>{format.number(trips)}</dd>
          </div>
          <div>
            <dt>{t('fleet.facts.invalid')}</dt>
            <dd>{format.number(invalid)}</dd>
          </div>
        </dl>
        <p className="mt-2 max-w-[80ch] text-sm text-ink-2">{t('fleet.note')}</p>
      </header>

      <div className={FILTER_BAR} role="group" aria-label={t('dates.filters')}>
        <DateRangePicker label={t('dates.period')} days={report.days} value={range} onChange={setRange} format={format} />
        <DayKindSelect />
      </div>

      {selected !== null && (
        <section ref={panel} tabIndex={-1} aria-labelledby="fleet-vehicle" className="mb-8 rounded-[10px] border border-route bg-route-soft/40 p-4 outline-none md:p-5">
          <div className="mb-3 flex items-start justify-between gap-4">
            <h2 id="fleet-vehicle" className="text-xl">
              {t('fleet.detail.heading', { vehicle: selected })}
            </h2>
            <button className={CLOSE} onClick={() => onSelect(null)} aria-label={t('fleet.detail.close')}>
              <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden="true">
                <path d="M6 6l12 12M18 6 6 18" />
              </svg>
            </button>
          </div>
          {/* Keyed: another vehicle starts again from its days. */}
          <VehicleDetailPanel key={selected} vehicle={selected} period={period} format={format} />
        </section>
      )}

      <VehicleTable vehicles={report.vehicles} selected={selected} format={format} onSelect={onSelect} />
    </div>
  )
}

const unknown = (t: TFunction, value: string | number | null) => (value === null || value === '' ? t('fleet.unknown') : value)

/** Traction in the UI language; ADA's "NA" (not filled in) reads as unknown. */
const traction = (t: TFunction, v: FleetVehicle) => (v.traction && v.traction !== 'NA' ? tractionLabel(t, v.traction) : null)

/** The register's columns for the CSV export: everything the table shows, plus seats and standing places apart. */
function vehicleCsvColumns(t: TFunction): CsvColumn<FleetVehicle>[] {
  return [
    { header: t('fleet.columns.vehicle'), value: (v) => v.id },
    { header: t('fleet.columns.traction'), value: (v) => traction(t, v) },
    { header: t('fleet.columns.model'), value: (v) => v.model },
    { header: t('fleet.columns.depot'), value: (v) => v.depot },
    { header: t('fleet.columns.capacity'), value: (v) => v.capacity },
    { header: t('fleet.columns.seating'), value: (v) => v.seatingCapacity },
    { header: t('fleet.columns.standing'), value: (v) => v.standingCapacity },
    { header: t('fleet.columns.devices'), value: (v) => v.devices },
    { header: t('fleet.columns.lastData'), value: (v) => v.lastData },
    { header: t('fleet.columns.days'), value: (v) => v.days },
    { header: t('fleet.columns.trips'), value: (v) => v.trips },
    { header: t('fleet.columns.invalid'), value: (v) => v.invalidTrips },
    { header: t('fleet.columns.boardings'), value: (v) => v.boardings },
    { header: t('fleet.columns.alightings'), value: (v) => v.alightings },
    { header: t('fleet.columns.faults'), value: (v) => v.faults },
    { header: t('fleet.columns.excluded'), value: (v) => (v.isExcluded ? t('fleet.excluded') : '') },
  ]
}

const vehicleCol = createColumnHelper<typeof sortableFeatures, FleetVehicle>()
function VehicleTable({
  vehicles,
  selected,
  format,
  onSelect,
}: {
  vehicles: FleetVehicle[]
  selected: number | null
  format: Format
  onSelect: (vehicle: number) => void
}) {
  const { t, i18n } = useTranslation()
  const [search, setSearch] = useState('')
  const rows = useMemo(
    () =>
      vehicles.filter((v) =>
        matchesRow(search, { exact: [v.id], texts: [v.model, v.depot, v.traction, traction(t, v)] }),
      ),
    [vehicles, search, t],
  )
  const columns = useMemo(
    () => [
      vehicleCol.accessor('id', {
        id: 'vehicle',
        header: t('fleet.columns.vehicle'),
        cell: (info) => (
          <>
            <LinkButton className="font-display text-[17px] leading-[1.1] font-bold" onClick={() => onSelect(info.getValue())} aria-label={t('fleet.openVehicle', { vehicle: info.getValue() })}>
              {info.getValue()}
            </LinkButton>
            {info.row.original.isExcluded && <span className="ml-2 text-xs text-ink-2">{t('fleet.excluded')}</span>}
          </>
        ),
      }),
      vehicleCol.accessor((v) => traction(t, v) ?? '', { id: 'traction', header: t('fleet.columns.traction'), cell: (info) => unknown(t, info.getValue()) }),
      vehicleCol.accessor((v) => v.model ?? '', { id: 'model', header: t('fleet.columns.model'), cell: (info) => unknown(t, info.getValue()) }),
      vehicleCol.accessor((v) => v.depot ?? '', { id: 'depot', header: t('fleet.columns.depot'), cell: (info) => unknown(t, info.getValue()) }),
      vehicleCol.accessor((v) => v.capacity ?? -1, { id: 'capacity', header: t('fleet.columns.capacity'), cell: (info) => unknown(t, info.row.original.capacity) }),
      vehicleCol.accessor('devices', { id: 'devices', header: t('fleet.columns.devices') }),
      vehicleCol.accessor((v) => v.lastData ?? '', {
        id: 'lastData',
        header: t('fleet.columns.lastData'),
        cell: (info) => <span className="tabular-nums">{info.row.original.lastData ? format.date(info.row.original.lastData) : t('fleet.unknown')}</span>,
      }),
      vehicleCol.accessor('days', { id: 'days', header: t('fleet.columns.days'), cell: (info) => format.number(info.getValue()) }),
      vehicleCol.accessor('trips', { id: 'trips', header: t('fleet.columns.trips'), cell: (info) => format.number(info.getValue()) }),
      vehicleCol.accessor('invalidTrips', { id: 'invalid', header: t('fleet.columns.invalid'), cell: (info) => format.number(info.getValue()) }),
      vehicleCol.accessor('boardings', { id: 'boardings', header: t('fleet.columns.boardings'), cell: (info) => format.number(info.getValue()) }),
      vehicleCol.accessor('alightings', { id: 'alightings', header: t('fleet.columns.alightings'), cell: (info) => format.number(info.getValue()) }),
      vehicleCol.accessor('faults', { id: 'faults', header: t('fleet.columns.faults'), cell: (info) => format.number(info.getValue()) }),
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps -- labels change with the language
    [i18n.resolvedLanguage, format, onSelect],
  )

  return (
    <section aria-label={t('fleet.title')}>
      <div role="search" className="mt-3 mb-2.5 flex flex-wrap items-center gap-3">
        <SearchInput label={t('dwell.search')} placeholder={t('fleet.search')} value={search} onChange={setSearch} />
        <span className="text-xs text-ink-2">{t('dwell.list.shown', { shown: format.number(rows.length), total: format.number(vehicles.length) })}</span>
        <button className={`${BUTTON} ml-auto`} onClick={() => downloadCsv(`${t('fleet.exportFile')}.csv`, vehicleCsvColumns(t), rows)}>
          {t('fleet.export')}
        </button>
      </div>
      <SortableTable
        columns={columns}
        numeric={['vehicle', 'capacity', 'devices', 'days', 'trips', 'invalid', 'boardings', 'alightings', 'faults']}
        data={rows}
        rowId={(v) => String(v.id)}
        sorting={[{ id: 'vehicle', desc: false }]}
        highlight={(v) => v.id === selected}
        empty={t('dwell.noMatch')}
      />
    </section>
  )
}

/** One vehicle: its details, days with trips (a day opens its trips), counting units and known faults. */
function VehicleDetailPanel({ vehicle, period, format }: { vehicle: number; period: Period; format: Format }) {
  const { t } = useTranslation()
  const detail = useQuery({ ...vehicleDetailQuery(vehicle, period), placeholderData: keepPreviousData })
  const [day, setDay] = useState<string | null>(null)
  return (
    <QueryState query={detail} loading={t('fleet.loading')}>
      {(data) => {
        const v = data.vehicle
        return (
          <div className="grid gap-6">
            <dl className="flex flex-wrap gap-x-7 gap-y-1.5 [&>div]:flex [&>div]:items-baseline [&>div]:gap-2 [&_dd]:font-semibold [&_dt]:text-ink-2">
              <div>
                <dt>{t('fleet.detail.facts.traction')}</dt>
                <dd>{traction(t, v) ?? t('fleet.unknown')}</dd>
              </div>
              <div>
                <dt>{t('fleet.detail.facts.model')}</dt>
                <dd>{unknown(t, v.model)}</dd>
              </div>
              <div>
                <dt>{t('fleet.detail.facts.depot')}</dt>
                <dd>{unknown(t, v.depot)}</dd>
              </div>
              <div>
                <dt>{t('fleet.detail.facts.capacity')}</dt>
                <dd>
                  {v.capacity === null
                    ? t('fleet.unknown')
                    : t('fleet.detail.facts.capacityValue', { total: v.capacity, seating: v.seatingCapacity, standing: v.standingCapacity })}
                </dd>
              </div>
              <div>
                <dt>{t('fleet.detail.facts.lastData')}</dt>
                <dd>{v.lastData ? format.date(v.lastData) : t('fleet.unknown')}</dd>
              </div>
            </dl>

            <DayTable vehicle={vehicle} days={data.tripDays} selected={day} format={format} onOpen={setDay} />

            {day !== null && (
              <section aria-labelledby="fleet-day" className="rounded-[10px] border border-rule bg-paper p-4">
                <h3 id="fleet-day" className="mb-3 text-lg">
                  {t('fleet.detail.day.heading', { vehicle, day: format.date(day) })}
                </h3>
                <VehicleDay vehicle={vehicle} day={day} format={format} onDayChange={setDay} />
              </section>
            )}

            <div className="grid gap-6 xl:grid-cols-2">
              <DeviceList devices={data.devices} format={format} />
              <FaultList faults={data.faults} format={format} />
            </div>
          </div>
        )
      }}
    </QueryState>
  )
}

function dayCsvColumns(t: TFunction): CsvColumn<VehicleDaySummary>[] {
  return [
    { header: t('fleet.detail.days.day'), value: (d) => d.day },
    { header: t('fleet.detail.days.first'), value: (d) => d.firstStart },
    { header: t('fleet.detail.days.last'), value: (d) => d.lastEnd },
    { header: t('fleet.detail.days.trips'), value: (d) => d.trips },
    { header: t('fleet.detail.days.invalid'), value: (d) => d.invalidTrips },
    { header: t('fleet.detail.days.depot'), value: (d) => d.depotRuns },
    { header: t('fleet.detail.days.boardings'), value: (d) => d.boardings },
    { header: t('fleet.detail.days.alightings'), value: (d) => d.alightings },
  ]
}

const dayCol = createColumnHelper<typeof sortableFeatures, VehicleDaySummary>()
function DayTable({
  vehicle,
  days,
  selected,
  format,
  onOpen,
}: {
  vehicle: number
  days: VehicleDaySummary[]
  selected: string | null
  format: Format
  onOpen: (day: string) => void
}) {
  const { t, i18n } = useTranslation()
  const columns = useMemo(
    () => [
      dayCol.accessor('day', {
        id: 'day',
        header: t('fleet.detail.days.day'),
        cell: (info) => (
          <LinkButton onClick={() => onOpen(info.getValue())} aria-label={t('fleet.detail.days.open', { day: format.date(info.getValue()) })}>
            {format.date(info.getValue())}
          </LinkButton>
        ),
      }),
      dayCol.accessor('firstStart', { id: 'first', header: t('fleet.detail.days.first'), cell: (info) => <span className="tabular-nums">{format.dateTime(info.getValue())}</span> }),
      dayCol.accessor('lastEnd', { id: 'last', header: t('fleet.detail.days.last'), cell: (info) => <span className="tabular-nums">{format.dateTime(info.getValue())}</span> }),
      dayCol.accessor('trips', { id: 'trips', header: t('fleet.detail.days.trips'), cell: (info) => format.number(info.getValue()) }),
      dayCol.accessor('invalidTrips', { id: 'invalid', header: t('fleet.detail.days.invalid'), cell: (info) => format.number(info.getValue()) }),
      dayCol.accessor('depotRuns', { id: 'depot', header: t('fleet.detail.days.depot'), cell: (info) => format.number(info.getValue()) }),
      dayCol.accessor('boardings', { id: 'boardings', header: t('fleet.detail.days.boardings'), cell: (info) => format.number(info.getValue()) }),
      dayCol.accessor('alightings', { id: 'alightings', header: t('fleet.detail.days.alightings'), cell: (info) => format.number(info.getValue()) }),
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps -- labels change with the language
    [i18n.resolvedLanguage, format, onOpen],
  )
  return (
    <section aria-labelledby="fleet-days">
      <div className="mb-2.5 flex flex-wrap items-end gap-3">
        <div>
          <h3 id="fleet-days" className="text-lg">
            {t('fleet.detail.days.title')}
          </h3>
          <p className="mt-0.5 text-sm text-ink-2">{t('fleet.detail.days.subtitle')}</p>
        </div>
        {days.length > 0 && (
          <button className={`${BUTTON} ml-auto`} onClick={() => downloadCsv(`${t('fleet.detail.days.exportFile', { vehicle })}.csv`, dayCsvColumns(t), days)}>
            {t('fleet.export')}
          </button>
        )}
      </div>
      <SortableTable
        columns={columns}
        numeric={['trips', 'invalid', 'depot', 'boardings', 'alightings']}
        data={days}
        rowId={(d) => d.day}
        sorting={[{ id: 'day', desc: false }]}
        highlight={(d) => d.day === selected}
        empty={t('fleet.detail.days.none')}
      />
    </section>
  )
}

const deviceCol = createColumnHelper<typeof sortableFeatures, VehicleDevice>()
function DeviceList({ devices, format }: { devices: VehicleDevice[]; format: Format }) {
  const { t, i18n } = useTranslation()
  const columns = useMemo(
    () => [
      deviceCol.accessor('deviceNumber', { id: 'number', header: t('fleet.detail.devices.number') }),
      deviceCol.accessor((d) => d.firmwareVersion ?? '', { id: 'firmware', header: t('fleet.detail.devices.firmware'), cell: (info) => unknown(t, info.getValue()) }),
      deviceCol.accessor('firstSeen', { id: 'first', header: t('fleet.detail.devices.firstSeen'), cell: (info) => <span className="tabular-nums">{format.dateTime(info.getValue())}</span> }),
      deviceCol.accessor('lastSeen', { id: 'last', header: t('fleet.detail.devices.lastSeen'), cell: (info) => <span className="tabular-nums">{format.dateTime(info.getValue())}</span> }),
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps -- labels change with the language
    [i18n.resolvedLanguage, format],
  )
  return (
    <section aria-labelledby="fleet-devices">
      <h3 id="fleet-devices" className="mb-2.5 text-lg">
        {t('fleet.detail.devices.title')}
      </h3>
      <SortableTable
        columns={columns}
        numeric={['number']}
        data={devices}
        rowId={(d) => String(d.deviceNumber)}
        sorting={[{ id: 'number', desc: false }]}
        empty={t('fleet.detail.devices.none')}
      />
    </section>
  )
}

const faultCol = createColumnHelper<typeof sortableFeatures, VehicleFault>()
function FaultList({ faults, format }: { faults: VehicleFault[]; format: Format }) {
  const { t, i18n } = useTranslation()
  const columns = useMemo(
    () => [
      faultCol.accessor((f) => f.deviceNumber ?? -1, {
        id: 'device',
        header: t('fleet.detail.faults.device'),
        cell: (info) => info.row.original.deviceNumber ?? t('fleet.detail.faults.wholeVehicle'),
      }),
      faultCol.accessor('from', { id: 'from', header: t('fleet.detail.faults.from'), cell: (info) => <span className="tabular-nums">{format.dateTime(info.getValue())}</span> }),
      faultCol.accessor((f) => f.to ?? '', {
        id: 'to',
        header: t('fleet.detail.faults.to'),
        cell: (info) => <span className="tabular-nums">{info.row.original.to ? format.dateTime(info.row.original.to) : t('fleet.detail.faults.ongoing')}</span>,
      }),
      faultCol.accessor((f) => t(`fleet.detail.faults.kinds.${f.kind}`), { id: 'kind', header: t('fleet.detail.faults.kind') }),
      faultCol.accessor((f) => t(`fleet.detail.faults.sources.${f.source}`), { id: 'source', header: t('fleet.detail.faults.source') }),
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps -- labels change with the language
    [i18n.resolvedLanguage, format],
  )
  return (
    <section aria-labelledby="fleet-faults">
      <h3 id="fleet-faults" className="text-lg">
        {t('fleet.detail.faults.title')}
      </h3>
      <p className="mt-0.5 mb-2.5 text-sm text-ink-2">{t('fleet.detail.faults.subtitle')}</p>
      <SortableTable
        columns={columns}
        numeric={['device']}
        data={faults}
        rowId={(f) => String(f.id)}
        sorting={[{ id: 'from', desc: false }]}
        empty={t('fleet.detail.faults.none')}
      />
    </section>
  )
}
