import { useQuery } from '@tanstack/react-query'
import { createColumnHelper } from '@tanstack/react-table'
import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { VehicleStop, VehicleTrip, VehicleTripsDay } from '../api'
import { ChartFigure } from '../charts/ChartFigure'
import { AXIS_LABEL, BASELINE, CHART_BOX, GRIDLINE, HIT_AREA, TOOLTIP_LABEL, TOOLTIP_VALUE } from '../charts/marks'
import { ChartTooltip } from '../charts/ChartTooltip'
import { FocusRing } from '../charts/FocusRing'
import { useElementWidth, useTooltip } from '../charts/useChart'
import { useRovingFocus } from '../charts/useRovingFocus'
import type { Format } from '../i18n/format'
import { vehicleDayQuery } from '../queries'
import { cn } from '../ui/cn'
import { Empty } from '../ui/Empty'
import { QueryState } from '../ui/QueryState'
import { Select } from '../ui/Select'
import { SortableTable } from './SortableTable'
import { sortableFeatures } from './tableFeatures'

const col = createColumnHelper<typeof sortableFeatures, VehicleStop>()
const NUMERIC = ['seq', 'dwell', 'boardings', 'alightings', 'occupancy', 'delay']

/** One vehicle's day: its trips, and for the chosen one a strip of every stop's dwell and passengers. */
export function VehicleDay({
  vehicle,
  day,
  at,
  tripId,
  format,
  onDayChange,
}: {
  vehicle: number
  day: string
  /** Open on the trip with an arrival at this time… */
  at?: string
  /** …or on this trip. */
  tripId?: number
  format: Format
  onDayChange: (day: string) => void
}) {
  const { t } = useTranslation()
  const query = useQuery(vehicleDayQuery(vehicle, day))
  return (
    <QueryState query={query} loading={t('dwell.loading')}>
      {(data) => <VehicleDayView key={`${vehicle}-${day}-${tripId ?? ''}`} data={data} at={at} openTripId={tripId} format={format} onDayChange={onDayChange} />}
    </QueryState>
  )
}

function VehicleDayView({ data, at, openTripId, format, onDayChange }: { data: VehicleTripsDay; at?: string; openTripId?: number; format: Format; onDayChange: (day: string) => void }) {
  const { t } = useTranslation()
  // Open on the trip that contains the arrival the reader came from.
  const initial = useMemo(() => {
    if (openTripId !== undefined && data.trips.some((x) => x.id === openTripId)) return openTripId
    if (!at) return data.trips[0]?.id
    return (data.trips.find((trip) => trip.stops.some((s) => s.arrival === at)) ?? data.trips[0])?.id
  }, [data.trips, at, openTripId])
  const [tripId, setTripId] = useState(initial)
  const trip = data.trips.find((x) => x.id === tripId)
  const time = (iso: string) => new Date(iso).toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' })

  return (
    <div className="grid gap-4">
      <div className="flex flex-wrap items-center gap-3">
        <Select label={t('dwell.day')} value={data.day} options={data.days.map((d) => ({ value: d, label: format.date(d) }))} onChange={onDayChange} />
      </div>
      {data.trips.length === 0 ? (
        <Empty>{t('dwell.vehicle.noTrips')}</Empty>
      ) : (
        <>
          <ul className="m-0 flex list-none flex-wrap gap-1.5 p-0" aria-label={t('dwell.vehicle.heading', { vehicle: data.vehicleId, day: format.date(data.day) })}>
            {data.trips.map((x) => (
              <li key={x.id}>
                <button
                  className={cn(
                    'inline-flex min-h-8 cursor-pointer items-center gap-1.5 touch-target rounded-lg border px-2.5 text-sm',
                    x.id === tripId ? 'border-route bg-route text-on-route' : 'border-rule bg-paper hover:border-ink-2',
                    !x.isValid && x.id !== tripId && 'text-ink-2',
                  )}
                  aria-pressed={x.id === tripId}
                  onClick={() => setTripId(x.id)}
                >
                  <span className="font-semibold tabular-nums">{time(x.start)}</span>
                  {x.line !== null && <span>{t('dwell.vehicle.tripLine', { line: x.line })}</span>}
                </button>
              </li>
            ))}
          </ul>
          {trip && <TripStrip trip={trip} format={format} />}
        </>
      )}
    </div>
  )
}

/** One trip stop by stop: each stop's dwell as a bar with its passengers below, and the stops as a table. */
export function TripStrip({ trip, format }: { trip: VehicleTrip; format: Format }) {
  const { t, i18n } = useTranslation()
  const signed = (seconds: number) => (seconds > 0 ? '+' : '') + format.seconds(seconds)
  const title = [
    t('dwell.vehicle.trip', { time: new Date(trip.start).toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' }) }),
    trip.line !== null ? t('dwell.vehicle.tripLine', { line: trip.line }) : null,
    trip.firstStopName && trip.lastStopName ? t('dwell.vehicle.tripRoute', { from: trip.firstStopName, to: trip.lastStopName }) : null,
  ].filter(Boolean).join(' · ')
  const notes = [!trip.isValid ? t('dwell.vehicle.invalid') : null, trip.isDepotRun ? t('dwell.vehicle.depot') : null].filter(Boolean).join(' · ')

  const columns = useMemo(
    () => [
      col.accessor('sequence', { id: 'seq', header: t('dwell.vehicle.seq') }),
      col.accessor((s) => s.stopName || String(s.stopCode), { id: 'stop', header: t('dwell.list.stop') }),
      col.accessor((s) => s.arrival ?? '', { id: 'arrival', header: t('dwell.list.time'), cell: (info) => (info.row.original.arrival ? format.dateTime(info.row.original.arrival) : '–') }),
      col.accessor((s) => s.dwellSeconds ?? -1, {
        id: 'dwell',
        header: t('dwell.list.dwell'),
        cell: (info) => (info.row.original.isPassThrough ? <span className="text-ink-2">{t('dwell.vehicle.pass')}</span> : info.row.original.dwellSeconds === null ? '–' : format.seconds(info.row.original.dwellSeconds)),
      }),
      col.accessor('boardings', { id: 'boardings', header: t('dwell.vehicle.boardings') }),
      col.accessor('alightings', { id: 'alightings', header: t('dwell.vehicle.alightings') }),
      col.accessor('occupancy', { id: 'occupancy', header: t('dwell.vehicle.occupancy') }),
      col.accessor('delaySeconds', { id: 'delay', header: t('dwell.list.delay'), cell: (info) => signed(info.getValue()) }),
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps -- labels change with the language
    [i18n.resolvedLanguage, format],
  )

  return (
    <ChartFigure
      title={title}
      subtitle={[t('dwell.vehicle.stripSubtitle'), notes].filter(Boolean).join(' ')}
      chart={<Strip stops={trip.stops} format={format} />}
      table={<SortableTable columns={columns} numeric={NUMERIC} data={trip.stops} rowId={(s) => String(s.sequence)} sorting={[{ id: 'seq', desc: false }]} pageSize={100} />}
    />
  )
}

const H = 170
const LEFT = 52
const TOP = 12
const BOTTOM = 40
const MIN_BAND = 26

function secondsStep(max: number) {
  return [5, 10, 15, 20, 30, 60, 120, 300, 600].find((s) => max / s <= 5) ?? 900
}

/** The trip's stops in order: bar = dwell, number below = boardings + alightings. Arrow keys move between stops. */
function Strip({ stops, format }: { stops: VehicleStop[]; format: Format }) {
  const { t } = useTranslation()
  const [wrap, available] = useElementWidth<HTMLDivElement>()
  const { box, tooltip, show, hide } = useTooltip()
  const roving = useRovingFocus(stops.map((_, i) => [0, i] as const))
  const width = available > 0 ? Math.max(available, LEFT + 12 + stops.length * MIN_BAND) : 0
  const band = (width - LEFT - 12) / Math.max(1, stops.length)
  const maxRaw = Math.max(30, ...stops.map((s) => s.dwellSeconds ?? 0))
  const step = secondsStep(maxRaw)
  const maxY = Math.ceil(maxRaw / step) * step
  const y = (s: number) => TOP + H - (s / maxY) * H
  const ticks = Array.from({ length: maxY / step + 1 }, (_, i) => i * step)
  const barW = Math.min(18, Math.max(4, band * 0.6))

  return (
    <div ref={wrap} className="min-w-0">
      <div ref={box} className={CHART_BOX}>
        {width > 0 && (
          <svg width={width} height={TOP + H + BOTTOM} role="group" aria-label={t('dwell.vehicle.stripKeys')}>
            {ticks.map((tick) => (
              <g key={tick}>
                <line x1={LEFT} x2={width - 12} y1={y(tick)} y2={y(tick)} className={tick === 0 ? BASELINE : GRIDLINE} />
                <text x={LEFT - 8} y={y(tick)} className={AXIS_LABEL} textAnchor="end" dominantBaseline="middle">
                  {format.seconds(tick)}
                </text>
              </g>
            ))}
            {stops.map((s, i) => {
              const cx = LEFT + i * band + band / 2
              const dwell = s.dwellSeconds ?? 0
              const label = t('dwell.vehicle.stopCell', {
                stop: s.stopName || s.stopCode,
                dwell: s.isPassThrough ? t('dwell.vehicle.pass') : format.seconds(dwell),
                boardings: s.boardings,
                alightings: s.alightings,
              })
              const content = (
                <>
                  <strong className={TOOLTIP_VALUE}>{s.isPassThrough ? t('dwell.vehicle.pass') : format.seconds(dwell)}</strong>
                  <span className={TOOLTIP_LABEL}>{label}</span>
                </>
              )
              const focus = roving.itemProps(0, i, { onFocus: (el) => show(el.getBoundingClientRect(), content), onBlur: hide })
              return (
                <g key={s.sequence}>
                  {!s.isPassThrough && dwell > 0 && <rect x={cx - barW / 2} y={y(dwell)} width={barW} height={y(0) - y(dwell)} rx={2} className="fill-series-1" />}
                  {s.isPassThrough && <circle cx={cx} cy={y(0) - 3} r={2.5} className="fill-ink-2" />}
                  <rect x={LEFT + i * band} y={TOP} width={band} height={H} className={HIT_AREA} {...focus} role="img" aria-label={label} onPointerMove={(event) => show(event, content)} onPointerLeave={hide} />
                  <FocusRing show={roving.isFocused(0, i)} x={LEFT + i * band + 1} y={TOP - 2} width={band - 2} height={H + 3} />
                  <text x={cx} y={TOP + H + 16} className={AXIS_LABEL} textAnchor="middle">
                    {s.isPassThrough ? '' : s.boardings + s.alightings}
                  </text>
                </g>
              )
            })}
            <text x={LEFT - 8} y={TOP + H + 16} className={AXIS_LABEL} textAnchor="end">
              {t('dwell.list.passengers')}
            </text>
          </svg>
        )}
        <ChartTooltip tooltip={tooltip} width={width} />
      </div>
    </div>
  )
}
