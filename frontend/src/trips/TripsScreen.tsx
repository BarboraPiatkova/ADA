import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { createColumnHelper } from '@tanstack/react-table'
import type { TFunction } from 'i18next'
import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { TripDetail, TripList, TripRow, VehicleStop } from '../api'
import { FaultList } from '../fleet/FaultList'
import { useFormat, type Format } from '../i18n/format'
import { numberParam, openScreen, useScreenParams } from '../navigation'
import { FILTER_BAR, usePeriod, useReportPeriod } from '../operations/period'
import type { RowFlag } from '../operations/RowFlag'
import { lineOptionMatch, matchesRow } from '../operations/shared'
import { SortableTable } from '../operations/SortableTable'
import { StopValueMap, type ValueStop } from '../operations/StopValueMap'
import { sortableFeatures } from '../operations/tableFeatures'
import { DayKindSelect } from '../operations/TimeView'
import { TripStrip } from '../operations/VehicleDay'
import { HealthSkeleton } from '../quality/HealthSkeleton'
import { mapConfigQuery, stopsQuery, tripQuery, tripsQuery } from '../queries'
import { downloadCsv, type CsvColumn } from '../ui/csv'
import { DateRangePicker } from '../ui/DateRangePicker'
import { Empty } from '../ui/Empty'
import { LinkButton } from '../ui/LinkButton'
import { QueryState } from '../ui/QueryState'
import { SearchInput } from '../ui/SearchInput'
import { SearchSelect } from '../ui/SearchSelect'
import { Select } from '../ui/Select'

const ALL = 'all'
const BUTTON = 'inline-flex h-8 cursor-pointer items-center touch-target rounded-lg border border-rule bg-paper px-3 text-sm text-ink hover:border-ink-2'
const CLOSE =
  'inline-flex size-8 shrink-0 cursor-pointer items-center justify-center touch-target rounded-lg border border-rule bg-paper text-ink-2 hover:border-ink-2 hover:text-ink'

/** Which trips the list shows. */
type Show = 'all' | 'valid' | 'invalid' | 'service'
const SHOWS: Show[] = ['all', 'valid', 'invalid', 'service']

/** At most two trips open at once: one to read, two to compare side by side. */
const MAX_OPEN = 2

const time = (iso: string) => new Date(iso).toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' })

/**
 * Every trip, as ADA's "Jízdy" listed them: invalid ones and depot runs included and marked, narrowed by
 * line, vehicle and period. A trip opens stop by stop, with why it is invalid, its overview and the map;
 * a second one opens beside it to compare. "#/jizdy?trip=123", "?vehicle=1083" and "?line=1" open on those.
 */
export function TripsScreen() {
  const { t } = useTranslation()
  const params = useScreenParams()
  const [line, setLine] = useState<number | null>(() => numberParam(params, 'line'))
  const [vehicle, setVehicle] = useState<number | null>(() => numberParam(params, 'vehicle'))
  const [open, setOpen] = useState<number[]>(() => {
    const trip = numberParam(params, 'trip')
    return trip === null ? [] : [trip]
  })
  const { period, isAll } = useReportPeriod()
  const report = useQuery({ ...tripsQuery(line, vehicle, period), placeholderData: keepPreviousData })
  return (
    <QueryState query={report} loading={t('trips.loading')} skeleton={<HealthSkeleton label={t('trips.loading')} />}>
      {(data) =>
        data.total === 0 && line === null && vehicle === null && isAll ? (
          <Empty>{t('trips.empty')}</Empty>
        ) : (
          <TripsView report={data} line={line} vehicle={vehicle} open={open} onLineChange={setLine} onVehicleChange={setVehicle} onOpenChange={setOpen} />
        )
      }
    </QueryState>
  )
}

function TripsView({
  report,
  line,
  vehicle,
  open,
  onLineChange,
  onVehicleChange,
  onOpenChange,
}: {
  report: TripList
  line: number | null
  vehicle: number | null
  open: number[]
  onLineChange: (line: number | null) => void
  onVehicleChange: (vehicle: number | null) => void
  onOpenChange: (open: number[]) => void
}) {
  const { t } = useTranslation()
  const format = useFormat()
  const [range, setRange] = usePeriod()
  const [show, setShow] = useState<Show>('all')
  const panel = useRef<HTMLElement>(null)

  const openTrip = useCallback((id: number) => onOpenChange([id]), [onOpenChange])
  const compareTrip = useCallback((id: number) => onOpenChange([...open.filter((x) => x !== id), id].slice(-MAX_OPEN)), [open, onOpenChange])
  const first = open[0]
  useEffect(() => {
    if (first === undefined) return
    panel.current?.scrollIntoView({ behavior: 'smooth', block: 'start' })
    panel.current?.focus({ preventScroll: true })
  }, [first])

  const shown = useMemo(
    () =>
      report.trips.filter((trip) =>
        show === 'valid' ? trip.isValid : show === 'invalid' ? !trip.isValid : show === 'service' ? !trip.isDepotRun : true,
      ),
    [report.trips, show],
  )
  const invalid = report.trips.filter((x) => !x.isValid).length
  const depot = report.trips.filter((x) => x.isDepotRun).length
  const boardings = report.trips.reduce((sum, x) => sum + x.boardings, 0)
  const lineOptions = [{ value: ALL, label: t('dwell.allLines') }, ...report.lines.map((l) => ({ value: String(l), label: t('dwell.lineN', { line: l }) }))]
  const vehicleOptions = [{ value: ALL, label: t('trips.allVehicles') }, ...report.vehicles.map((v) => ({ value: String(v), label: t('trips.vehicleN', { vehicle: v }) }))]

  return (
    <div className="flex-1 overflow-y-auto p-4 md:px-7 md:pt-6 md:pb-10">
      <header>
        <h1 className="mb-2 text-2xl">{t('trips.title')}</h1>
        <dl className="flex flex-wrap gap-x-7 gap-y-1.5 [&>div]:flex [&>div]:items-baseline [&>div]:gap-2 [&_dd]:font-semibold [&_dt]:text-ink-2">
          <div>
            <dt>{t('trips.facts.period')}</dt>
            <dd>
              {report.from ? format.date(report.from) : '?'} – {report.to ? format.date(report.to) : '?'}
            </dd>
          </div>
          <div>
            <dt>{t('trips.facts.trips')}</dt>
            <dd>{format.number(report.total)}</dd>
          </div>
          <div>
            <dt>{t('trips.facts.invalid')}</dt>
            <dd>{format.number(invalid)}</dd>
          </div>
          <div>
            <dt>{t('trips.facts.depot')}</dt>
            <dd>{format.number(depot)}</dd>
          </div>
          <div>
            <dt>{t('trips.facts.boardings')}</dt>
            <dd>{format.number(boardings)}</dd>
          </div>
        </dl>
        <p className="mt-2 max-w-[80ch] text-sm text-ink-2">{t('trips.note')}</p>
        {report.total > report.trips.length && (
          <p className="mt-1 text-sm font-semibold text-warning">{t('trips.truncated', { shown: format.number(report.trips.length), total: format.number(report.total) })}</p>
        )}
      </header>

      <div className={FILTER_BAR} role="group" aria-label={t('dates.filters')}>
        <SearchSelect
          label={t('dwell.line')}
          value={line === null ? ALL : String(line)}
          options={lineOptions}
          placeholder={t('dwell.searchLine')}
          empty={t('dwell.noOption')}
          match={lineOptionMatch}
          onChange={(v) => onLineChange(v === ALL ? null : Number(v))}
        />
        <SearchSelect
          label={t('trips.vehicle')}
          value={vehicle === null ? ALL : String(vehicle)}
          options={vehicleOptions}
          placeholder={t('trips.searchVehicle')}
          empty={t('dwell.noOption')}
          onChange={(v) => onVehicleChange(v === ALL ? null : Number(v))}
        />
        <DateRangePicker label={t('dates.period')} days={report.days} value={range} onChange={setRange} format={format} />
        <DayKindSelect />
        <Select label={t('trips.show')} value={show} options={SHOWS.map((s) => ({ value: s, label: t(`trips.shows.${s}`) }))} onChange={(v) => setShow(v as Show)} />
      </div>

      {open.length > 0 && (
        <section ref={panel} tabIndex={-1} aria-label={t('trips.title')} className="mb-8 grid gap-5 outline-none xl:grid-cols-[repeat(auto-fit,minmax(0,1fr))]">
          {open.map((id) => (
            <TripPanel key={id} id={id} format={format} onClose={() => onOpenChange(open.filter((x) => x !== id))} />
          ))}
        </section>
      )}

      <TripTable trips={shown} open={open} format={format} onOpen={openTrip} onCompare={compareTrip} />
    </div>
  )
}

function tripCsvColumns(t: TFunction): CsvColumn<TripRow>[] {
  return [
    { header: t('trips.columns.start'), value: (x) => x.start },
    { header: t('trips.columns.end'), value: (x) => x.end },
    { header: t('trips.columns.vehicle'), value: (x) => (x.secondVehicleId === null ? x.vehicleId : `${x.vehicleId}+${x.secondVehicleId}`) },
    { header: t('trips.columns.line'), value: (x) => x.line },
    { header: t('trips.columns.block'), value: (x) => x.block },
    { header: t('trips.columns.route'), value: (x) => `${x.firstStopName ?? '?'} – ${x.lastStopName ?? '?'}` },
    { header: t('trips.columns.stops'), value: (x) => x.stops },
    { header: t('trips.columns.boardings'), value: (x) => x.boardings },
    { header: t('trips.columns.alightings'), value: (x) => x.alightings },
    { header: t('trips.columns.status'), value: (x) => statusText(t, x) },
  ]
}

function statusText(t: TFunction, x: { isValid: boolean; isDepotRun: boolean }) {
  return [t(x.isValid ? 'trips.status.valid' : 'trips.status.invalid'), x.isDepotRun ? t('trips.status.depot') : null].filter(Boolean).join(', ')
}

function invalidFlag(t: TFunction, x: TripRow): RowFlag | null {
  if (x.isValid) return null
  return { status: 'Fault', reason: x.flaggedStops > 0 ? t('trips.flagInvalid', { count: x.flaggedStops }) : t('trips.flagInvalidOther') }
}

const tripCol = createColumnHelper<typeof sortableFeatures, TripRow>()
function TripTable({
  trips,
  open,
  format,
  onOpen,
  onCompare,
}: {
  trips: TripRow[]
  open: number[]
  format: Format
  onOpen: (id: number) => void
  onCompare: (id: number) => void
}) {
  const { t, i18n } = useTranslation()
  const [search, setSearch] = useState('')
  const rows = useMemo(
    () =>
      trips.filter(
        (x) =>
          matchesRow(search, { exact: [x.vehicleId, x.secondVehicleId, x.line], texts: [x.firstStopName, x.lastStopName] }) ||
          // Blocks are digits with leading zeros ("01200715"): matched as text, anywhere in the code.
          (search.trim() !== '' && (x.block?.includes(search.trim()) ?? false)),
      ),
    [trips, search],
  )
  const comparing = open.length > 0
  const columns = useMemo(
    () => [
      tripCol.accessor('start', {
        id: 'start',
        header: t('trips.columns.start'),
        cell: (info) => (
          <LinkButton className="tabular-nums" onClick={() => onOpen(info.row.original.id)} aria-label={t('trips.open', { time: format.dateTime(info.getValue()) })}>
            {format.dateTime(info.getValue())}
          </LinkButton>
        ),
      }),
      tripCol.accessor('vehicleId', {
        id: 'vehicle',
        header: t('trips.columns.vehicle'),
        cell: (info) => (
          <>
            <LinkButton className="font-display text-[17px] leading-[1.1] font-bold" onClick={() => openScreen('vozidla', { vehicle: info.getValue() })} aria-label={t('trips.openVehicle', { vehicle: info.getValue() })}>
              {info.getValue()}
            </LinkButton>
            {info.row.original.secondVehicleId !== null && <span className="text-ink-2"> + {info.row.original.secondVehicleId}</span>}
          </>
        ),
      }),
      tripCol.accessor((x) => x.line ?? -1, { id: 'line', header: t('trips.columns.line'), cell: (info) => info.row.original.line ?? '–' }),
      tripCol.accessor((x) => x.block ?? '', { id: 'block', header: t('trips.columns.block'), cell: (info) => info.getValue() || '–' }),
      tripCol.accessor((x) => `${x.firstStopName ?? '?'} – ${x.lastStopName ?? '?'}`, { id: 'route', header: t('trips.columns.route') }),
      tripCol.accessor('stops', { id: 'stops', header: t('trips.columns.stops') }),
      tripCol.accessor('boardings', { id: 'boardings', header: t('trips.columns.boardings'), cell: (info) => format.number(info.getValue()) }),
      tripCol.accessor('alightings', { id: 'alightings', header: t('trips.columns.alightings'), cell: (info) => format.number(info.getValue()) }),
      tripCol.accessor((x) => statusText(t, x), {
        id: 'status',
        header: t('trips.columns.status'),
        cell: (info) => <span className={info.row.original.isValid ? 'text-ink-2' : 'font-semibold text-fault'}>{info.getValue()}</span>,
      }),
      tripCol.display({
        id: 'compare',
        header: t('trips.columns.compare'),
        cell: (info) =>
          comparing && !open.includes(info.row.original.id) ? (
            <LinkButton className="text-xs whitespace-nowrap" onClick={() => onCompare(info.row.original.id)} aria-label={t('trips.compareTitle', { time: format.dateTime(info.row.original.start) })}>
              {t('trips.compare')}
            </LinkButton>
          ) : null,
      }),
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps -- labels change with the language
    [i18n.resolvedLanguage, format, onOpen, onCompare, comparing, open],
  )

  return (
    <section aria-labelledby="trips-list">
      <h2 id="trips-list" className="sr-only">
        {t('trips.title')}
      </h2>
      <div role="search" className="mt-3 mb-2.5 flex flex-wrap items-center gap-3">
        <SearchInput label={t('dwell.search')} placeholder={t('trips.search')} value={search} onChange={setSearch} />
        <span className="text-xs text-ink-2">{t('dwell.list.shown', { shown: format.number(rows.length), total: format.number(trips.length) })}</span>
        <button className={`${BUTTON} ml-auto`} onClick={() => downloadCsv(`${t('trips.exportFile')}.csv`, tripCsvColumns(t), rows)}>
          {t('trips.export')}
        </button>
      </div>
      <SortableTable
        columns={columns}
        numeric={['vehicle', 'line', 'stops', 'boardings', 'alightings']}
        data={rows}
        rowId={(x) => String(x.id)}
        sorting={[{ id: 'start', desc: false }]}
        highlight={(x) => open.includes(x.id)}
        flag={(x) => invalidFlag(t, x)}
        empty={t('dwell.noMatch')}
      />
    </section>
  )
}

/** One open trip: its facts and overview, why it is invalid, its faults, the stop-by-stop strip and the map. */
function TripPanel({ id, format, onClose }: { id: number; format: Format; onClose: () => void }) {
  const { t } = useTranslation()
  const detail = useQuery(tripQuery(id))
  return (
    <article aria-labelledby={`trip-${id}`} className="min-w-0 rounded-[10px] border border-route bg-route-soft/40 p-4 md:p-5">
      <QueryState query={detail} loading={t('trips.loading')}>
        {(data) => (
          <>
            <div className="mb-3 flex items-start justify-between gap-4">
              <h2 id={`trip-${id}`} className="text-xl">
                {t('trips.detail.heading', { time: `${format.date(data.trip.start.slice(0, 10))} ${time(data.trip.start)}`, vehicle: data.vehicleId })}
              </h2>
              <button className={CLOSE} onClick={onClose} aria-label={t('trips.detail.close')}>
                <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden="true">
                  <path d="M6 6l12 12M18 6 6 18" />
                </svg>
              </button>
            </div>
            <TripDetailView detail={data} format={format} />
          </>
        )}
      </QueryState>
    </article>
  )
}

function stopCsvColumns(t: TFunction): CsvColumn<VehicleStop>[] {
  return [
    { header: t('dwell.vehicle.seq'), value: (s) => s.sequence },
    { header: t('trips.detail.flagged.stop'), value: (s) => s.stopName || s.stopCode },
    { header: t('dwell.list.time'), value: (s) => s.arrival },
    { header: t('dwell.list.dwell'), value: (s) => (s.isPassThrough ? t('dwell.vehicle.pass') : s.dwellSeconds) },
    { header: t('dwell.vehicle.boardings'), value: (s) => s.boardings },
    { header: t('dwell.vehicle.alightings'), value: (s) => s.alightings },
    { header: t('dwell.vehicle.occupancy'), value: (s) => s.occupancy },
    { header: t('dwell.list.delay'), value: (s) => s.delaySeconds },
  ]
}

function TripDetailView({ detail, format }: { detail: TripDetail; format: Format }) {
  const { t } = useTranslation()
  const { trip, overview } = detail
  const km = (value: number | null) => (value === null ? '–' : format.decimal(value))
  const names = new Map(trip.stops.map((s) => [s.sequence, s.stopName || String(s.stopCode)]))
  return (
    <div className="grid gap-5">
      <dl className="flex flex-wrap gap-x-7 gap-y-1.5 [&>div]:flex [&>div]:items-baseline [&>div]:gap-2 [&_dd]:font-semibold [&_dt]:text-ink-2">
        <div>
          <dt>{t('trips.detail.facts.vehicle')}</dt>
          <dd>
            <LinkButton onClick={() => openScreen('vozidla', { vehicle: detail.vehicleId })} aria-label={t('trips.openVehicle', { vehicle: detail.vehicleId })}>
              {detail.vehicleId}
            </LinkButton>
            {detail.secondVehicleId !== null && <span className="font-normal text-ink-2"> ({t('trips.detail.facts.coupled', { vehicle: detail.secondVehicleId })})</span>}
          </dd>
        </div>
        <div>
          <dt>{t('trips.detail.facts.line')}</dt>
          <dd>{trip.line ?? '–'}</dd>
        </div>
        <div>
          <dt>{t('trips.detail.facts.route')}</dt>
          <dd>
            {trip.firstStopName ?? '?'} – {trip.lastStopName ?? '?'}
          </dd>
        </div>
        <div>
          <dt>{t('trips.detail.facts.block')}</dt>
          <dd>{detail.block ?? '–'}</dd>
        </div>
        <div>
          <dt>{t('trips.detail.facts.time')}</dt>
          <dd className="tabular-nums">
            {time(trip.start)} – {time(trip.end)}
          </dd>
        </div>
        <div>
          <dt>{t('trips.detail.facts.status')}</dt>
          <dd className={trip.isValid ? undefined : 'text-fault'}>{statusText(t, trip)}</dd>
        </div>
        <div>
          <dt>{t('trips.detail.facts.capacity')}</dt>
          <dd>{detail.capacity ?? '–'}</dd>
        </div>
      </dl>
      <dl className="flex flex-wrap gap-x-7 gap-y-1.5 [&>div]:flex [&>div]:items-baseline [&>div]:gap-2 [&_dd]:font-semibold [&_dt]:text-ink-2">
        <div>
          <dt>{t('trips.detail.overview.km')}</dt>
          <dd>{km(overview.km)}</dd>
        </div>
        <div>
          <dt>{t('trips.detail.overview.vehicleKm')}</dt>
          <dd>{km(overview.vehicleKm)}</dd>
        </div>
        <div>
          <dt>{t('trips.detail.overview.placeKm')}</dt>
          <dd>{km(overview.placeKm)}</dd>
        </div>
        <div>
          <dt>{t('trips.detail.overview.passengers')}</dt>
          <dd>{format.number(overview.passengers)}</dd>
        </div>
      </dl>

      {!trip.isValid && (
        <section aria-labelledby={`trip-${trip.id}-flagged`}>
          <h3 id={`trip-${trip.id}-flagged`} className="mb-1.5 text-lg">
            {t('trips.detail.flagged.title')}
          </h3>
          {detail.flaggedStops.length === 0 ? (
            <p className="m-0 text-sm text-ink-2">{t('trips.detail.flagged.none')}</p>
          ) : (
            <ul className="m-0 list-none p-0 text-sm">
              {detail.flaggedStops.map((s) => (
                <li key={s.sequence}>
                  <span className="text-ink-2">{s.sequence}.</span> {names.get(s.sequence) ?? s.stopCode}:{' '}
                  <span className="font-semibold">
                    {t('trips.detail.flagged.units')} {s.deviceNumbers.join(', ')}
                  </span>
                </li>
              ))}
            </ul>
          )}
        </section>
      )}

      <div className="flex justify-end">
        <button className={BUTTON} onClick={() => downloadCsv(`${t('trips.detail.exportFile', { id: trip.id })}.csv`, stopCsvColumns(t), trip.stops)}>
          {t('trips.export')}
        </button>
      </div>
      <TripStrip trip={trip} format={format} />
      <TripMap detail={detail} format={format} />
      {(detail.faults.length > 0 || !trip.isValid) && <FaultList faults={detail.faults} format={format} subtitle={t('trips.detail.faultsSubtitle')} />}
    </div>
  )
}

/**
 * ADA's "Vložit do mapy": the trip's stops on the map, sized by passengers changing and coloured by the
 * load after the stop. Needs the network (stops' positions); without access to it the map is left out.
 */
function TripMap({ detail, format }: { detail: TripDetail; format: Format }) {
  const { t } = useTranslation()
  const mapConfig = useQuery(mapConfigQuery)
  const network = useQuery(stopsQuery)
  const { stops, stepSize } = useMemo(() => {
    const byCode = new Map((network.data ?? []).map((s) => [s.code, s]))
    // Load after each stop: boardings minus alightings from the trip's start, never below zero.
    const loads = detail.trip.stops.reduce<number[]>((acc, s) => [...acc, Math.max(0, (acc.at(-1) ?? 0) + s.boardings - s.alightings)], [])
    // Five equal whole-number steps up to the peak load, so no two legend labels are the same.
    const size = Math.max(1, Math.ceil(Math.max(...loads, 0) / 5))
    const placed = detail.trip.stops
      .map((s, i): ValueStop | null => {
        const at = byCode.get(s.stopCode)
        if (!at) return null
        return {
          code: s.stopCode,
          name: s.stopName || at.name,
          toward: at.toward,
          bearing: at.bearing,
          latitude: at.latitude,
          longitude: at.longitude,
          size: s.boardings + s.alightings,
          step: Math.min(5, 1 + Math.floor(loads[i] / size)),
          detail: `${t('dwell.vehicle.boardings')} ${s.boardings} · ${t('dwell.vehicle.alightings')} ${s.alightings} · ${t('trips.detail.map.legend')} ${loads[i]}`,
        }
      })
      .filter((s): s is ValueStop => s !== null)
    // A loop calls at the same stop twice: one marker, both calls in it.
    const merged = new Map<number, ValueStop>()
    for (const s of placed) {
      const seen = merged.get(s.code)
      merged.set(s.code, seen ? { ...seen, size: seen.size + s.size, step: Math.max(seen.step, s.step), detail: `${seen.detail} | ${s.detail}` } : s)
    }
    return { stops: [...merged.values()], stepSize: size }
  }, [network.data, detail.trip.stops, t])
  if (!mapConfig.data || stops.length === 0) return null
  const legend = Array.from({ length: 5 }, (_, i) => (i === 4 ? `≥ ${format.number(i * stepSize)}` : `${format.number(i * stepSize)} – ${format.number((i + 1) * stepSize - 1)}`))
  return (
    <StopValueMap
      layers={mapConfig.data.baseLayers}
      stops={stops}
      title={t('trips.detail.map.title')}
      subtitle={t('trips.detail.map.subtitle')}
      legendTitle={t('trips.detail.map.legend')}
      legendLabels={legend}
    />
  )
}
