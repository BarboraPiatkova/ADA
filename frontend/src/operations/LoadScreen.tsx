import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { createColumnHelper } from '@tanstack/react-table'
import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { CrowdedTrip, LoadProfileStop, LoadReport } from '../api'
import { ChartFigure } from '../charts/ChartFigure'
import { useFormat, type Format } from '../i18n/format'
import { loadQuery } from '../queries'
import { HealthSkeleton } from '../quality/HealthSkeleton'
import { Empty } from '../ui/Empty'
import { LinkButton } from '../ui/LinkButton'
import { QueryState } from '../ui/QueryState'
import { SearchInput } from '../ui/SearchInput'
import { Select } from '../ui/Select'
import { ColumnChart } from './BarCharts'
import { dayOf, matchesRow } from './shared'
import { SortableTable } from './SortableTable'
import { sortableFeatures } from './tableFeatures'
import { VehicleDay } from './VehicleDay'

const ALL = 'all'

/** How full vehicles are: boardings over the day, a pattern's load stop by stop, and the fullest trips. */
export function LoadScreen() {
  const { t } = useTranslation()
  const [line, setLine] = useState<number | null>(null)
  const [pattern, setPattern] = useState<number | null>(null)
  const report = useQuery({ ...loadQuery(line, pattern), placeholderData: keepPreviousData })
  return (
    <QueryState query={report} loading={t('load.loading')} skeleton={<HealthSkeleton label={t('load.loading')} />}>
      {(data) =>
        data.trips === 0 && line === null ? (
          <Empty>{t('load.empty')}</Empty>
        ) : (
          <LoadView
            report={data}
            line={line}
            onLineChange={(l) => {
              setLine(l)
              setPattern(null)
            }}
            onPatternChange={setPattern}
          />
        )
      }
    </QueryState>
  )
}

function LoadView({
  report,
  line,
  onLineChange,
  onPatternChange,
}: {
  report: LoadReport
  line: number | null
  onLineChange: (line: number | null) => void
  onPatternChange: (pattern: number | null) => void
}) {
  const { t } = useTranslation()
  const format = useFormat()
  const [vehicleDay, setVehicleDay] = useState<{ vehicle: number; day: string; tripId?: number } | null>(null)
  const panel = useRef<HTMLElement>(null)
  const openVehicle = useCallback((vehicle: number, day: string, tripId: number) => setVehicleDay({ vehicle, day, tripId }), [])
  useEffect(() => {
    if (!vehicleDay) return
    panel.current?.scrollIntoView({ behavior: 'smooth', block: 'start' })
    panel.current?.focus({ preventScroll: true })
  }, [vehicleDay])

  const lineOptions = [{ value: ALL, label: t('dwell.allLines') }, ...report.lines.map((l) => ({ value: String(l), label: t('dwell.lineN', { line: l }) }))]
  const patternOptions = report.patterns.map((p) => ({
    value: String(p.code),
    label: t('load.patternOption', { from: p.firstStopName ?? '?', to: p.lastStopName ?? '?', trips: p.trips }),
  }))
  const chosen = report.patterns.find((p) => p.code === report.pattern)

  return (
    <div className="flex-1 overflow-y-auto p-4 md:px-7 md:pt-6 md:pb-10">
      <header>
        <h1 className="mb-2 text-2xl">{t('load.title')}</h1>
        <dl className="flex flex-wrap gap-x-7 gap-y-1.5 [&>div]:flex [&>div]:items-baseline [&>div]:gap-2 [&_dd]:font-semibold [&_dt]:text-ink-2">
          <div>
            <dt>{t('load.facts.period')}</dt>
            <dd>
              {report.from ? format.date(report.from) : '?'} – {report.to ? format.date(report.to) : '?'}
            </dd>
          </div>
          <div>
            <dt>{t('load.facts.trips')}</dt>
            <dd>{format.number(report.trips)}</dd>
          </div>
          <div>
            <dt>{t('load.facts.boardings')}</dt>
            <dd>{format.number(report.boardings)}</dd>
          </div>
          <div>
            <dt>{t('load.facts.capacity')}</dt>
            <dd>{format.number(report.tripsWithCapacity)}</dd>
          </div>
        </dl>
        <p className="mt-3 max-w-[72ch] text-sm text-ink-2">{t('load.note')}</p>
      </header>

      <div className="my-5 flex flex-wrap items-center gap-3">
        <Select label={t('dwell.line')} value={line === null ? ALL : String(line)} options={lineOptions} onChange={(v) => onLineChange(v === ALL ? null : Number(v))} />
        {patternOptions.length > 0 && report.pattern !== null && (
          <Select label={t('load.pattern')} value={String(report.pattern)} options={patternOptions} onChange={(v) => onPatternChange(Number(v))} />
        )}
      </div>

      <div className="mb-6 grid gap-5 xl:grid-cols-2">
        <ChartFigure
          title={t('load.hours.title')}
          subtitle={t('load.hours.subtitle')}
          chart={
            <ColumnChart
              keys={t('load.hours.keys')}
              formatValue={format.number}
              columns={report.boardingsByHour.map((h) => ({
                key: h.hour,
                label: String(h.hour),
                value: h.boardings,
                description: t('load.hours.cell', { hour: h.hour, boardings: format.number(h.boardings), alightings: format.number(h.alightings) }),
              }))}
            />
          }
          table={
            <SortableTable
              columns={hourColumns(t, format)}
              numeric={['boardings', 'alightings']}
              data={report.boardingsByHour}
              rowId={(h) => String(h.hour)}
              sorting={[{ id: 'hour', desc: false }]}
              pageSize={24}
            />
          }
        />
        <ChartFigure
          title={`${t('load.profile.title')}${chosen ? ` – ${chosen.firstStopName ?? '?'} → ${chosen.lastStopName ?? '?'}` : ''}`}
          subtitle={t('load.profile.subtitle')}
          chart={
            report.profile.length === 0 ? (
              <p className="text-sm text-ink-2">{t('load.profile.none')}</p>
            ) : (
              <ColumnChart
                keys={t('load.profile.keys')}
                formatValue={format.number}
                columns={report.profile.map((s) => ({
                  key: s.sequence,
                  label: String(s.sequence),
                  value: s.medianLoad,
                  whisker: s.p90Load,
                  description: t('load.profile.cell', { stop: s.stopName || s.stopCode, median: format.decimal(s.medianLoad), p90: format.decimal(s.p90Load) }),
                }))}
              />
            )
          }
          table={<ProfileTable profile={report.profile} format={format} />}
        />
      </div>

      {vehicleDay && (
        <section ref={panel} tabIndex={-1} aria-labelledby="load-vehicle" className="mb-8 rounded-[10px] border border-route bg-route-soft/40 p-4 outline-none md:p-5">
          <div className="mb-3 flex items-start justify-between gap-4">
            <h2 id="load-vehicle" className="text-xl">
              {t('dwell.vehicle.heading', { vehicle: vehicleDay.vehicle, day: format.date(vehicleDay.day) })}
            </h2>
            <button
              className="inline-flex size-8 shrink-0 cursor-pointer items-center justify-center touch-target rounded-lg border border-rule bg-paper text-ink-2 hover:border-ink-2 hover:text-ink"
              onClick={() => setVehicleDay(null)}
              aria-label={t('dwell.close')}
            >
              <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden="true">
                <path d="M6 6l12 12M18 6 6 18" />
              </svg>
            </button>
          </div>
          <VehicleDay vehicle={vehicleDay.vehicle} day={vehicleDay.day} tripId={vehicleDay.tripId} format={format} onDayChange={(day) => setVehicleDay({ vehicle: vehicleDay.vehicle, day })} />
        </section>
      )}

      <CrowdedTrips trips={report.crowded} format={format} onOpenVehicle={openVehicle} />
    </div>
  )
}

const hourCol = createColumnHelper<typeof sortableFeatures, LoadReport['boardingsByHour'][number]>()
function hourColumns(t: ReturnType<typeof useTranslation>['t'], format: Format) {
  return [
    hourCol.accessor('hour', { id: 'hour', header: t('load.hours.hour'), cell: (info) => `${info.getValue()}:00` }),
    hourCol.accessor('boardings', { id: 'boardings', header: t('load.hours.boardings'), cell: (info) => format.number(info.getValue()) }),
    hourCol.accessor('alightings', { id: 'alightings', header: t('load.hours.alightings'), cell: (info) => format.number(info.getValue()) }),
  ]
}

const profileCol = createColumnHelper<typeof sortableFeatures, LoadProfileStop>()
function ProfileTable({ profile, format }: { profile: LoadProfileStop[]; format: Format }) {
  const { t, i18n } = useTranslation()
  const columns = useMemo(
    () => [
      profileCol.accessor('sequence', { id: 'seq', header: t('dwell.vehicle.seq') }),
      profileCol.accessor((s) => s.stopName || String(s.stopCode), { id: 'stop', header: t('load.profile.stop') }),
      profileCol.accessor('trips', { id: 'trips', header: t('load.profile.trips'), cell: (info) => format.number(info.getValue()) }),
      profileCol.accessor('meanBoardings', { id: 'boardings', header: t('load.profile.boardings'), cell: (info) => format.decimal(info.getValue()) }),
      profileCol.accessor('meanAlightings', { id: 'alightings', header: t('load.profile.alightings'), cell: (info) => format.decimal(info.getValue()) }),
      profileCol.accessor('medianLoad', { id: 'median', header: t('load.profile.median'), cell: (info) => <span className="font-semibold">{format.decimal(info.getValue())}</span> }),
      profileCol.accessor('p90Load', { id: 'p90', header: t('load.profile.p90'), cell: (info) => format.decimal(info.getValue()) }),
      profileCol.accessor('maxLoad', { id: 'max', header: t('load.profile.max'), cell: (info) => format.number(info.getValue()) }),
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps -- labels change with the language
    [i18n.resolvedLanguage, format],
  )
  return (
    <SortableTable
      columns={columns}
      numeric={['seq', 'trips', 'boardings', 'alightings', 'median', 'p90', 'max']}
      data={profile}
      rowId={(s) => String(s.sequence)}
      sorting={[{ id: 'seq', desc: false }]}
      pageSize={100}
    />
  )
}

const tripCol = createColumnHelper<typeof sortableFeatures, CrowdedTrip>()
function CrowdedTrips({ trips, format, onOpenVehicle }: { trips: CrowdedTrip[]; format: Format; onOpenVehicle: (vehicle: number, day: string, tripId: number) => void }) {
  const { t, i18n } = useTranslation()
  const [search, setSearch] = useState('')
  const rows = useMemo(
    () =>
      trips.filter((r) =>
        matchesRow(search, { exact: [r.vehicleId, r.line], prefix: [r.peakStopCode], texts: [r.firstStopName, r.lastStopName, r.peakStopName] }),
      ),
    [trips, search],
  )
  const columns = useMemo(
    () => [
      tripCol.accessor('start', { id: 'start', header: t('load.crowded.start'), cell: (info) => <span className="tabular-nums">{format.dateTime(info.getValue())}</span> }),
      tripCol.accessor('vehicleId', {
        id: 'vehicle',
        header: t('load.crowded.vehicle'),
        cell: (info) => {
          const trip = info.row.original
          return (
            <LinkButton className="font-display text-[17px] leading-[1.1] font-bold" onClick={() => onOpenVehicle(trip.vehicleId, dayOf(trip.start), trip.tripId)} aria-label={t('dwell.openVehicle', { vehicle: trip.vehicleId })}>
              {trip.vehicleId}
            </LinkButton>
          )
        },
      }),
      tripCol.accessor((r) => r.line ?? -1, { id: 'line', header: t('load.crowded.line'), cell: (info) => info.row.original.line ?? t('load.crowded.unknown') }),
      tripCol.accessor((r) => `${r.firstStopName ?? '?'} → ${r.lastStopName ?? '?'}`, { id: 'route', header: t('load.crowded.route') }),
      tripCol.accessor('peakLoad', { id: 'peak', header: t('load.crowded.peak'), cell: (info) => <span className="font-semibold">{format.number(info.getValue())}</span> }),
      tripCol.accessor((r) => r.peakStopName ?? '', { id: 'at', header: t('load.crowded.at') }),
      tripCol.accessor('boardings', { id: 'boardings', header: t('load.crowded.boardings'), cell: (info) => format.number(info.getValue()) }),
      tripCol.accessor((r) => r.peakShare ?? -1, {
        id: 'share',
        header: t('load.crowded.share'),
        cell: (info) => (info.row.original.peakShare === null ? t('load.crowded.unknown') : format.percentWhole(info.row.original.peakShare)),
      }),
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps -- labels change with the language
    [i18n.resolvedLanguage, format, onOpenVehicle],
  )

  return (
    <section aria-labelledby="load-crowded">
      <h2 id="load-crowded" className="text-xl">
        {t('load.crowded.title')}
      </h2>
      <p className="mt-0.5 text-sm text-ink-2">{t('load.crowded.subtitle')}</p>
      <div role="search" className="mt-3 mb-2.5 flex flex-wrap items-center gap-3">
        <SearchInput label={t('dwell.search')} placeholder={t('load.crowded.search')} value={search} onChange={setSearch} />
        <span className="text-xs text-ink-2">{t('dwell.list.shown', { shown: format.number(rows.length), total: format.number(trips.length) })}</span>
      </div>
      <SortableTable
        columns={columns}
        numeric={['vehicle', 'line', 'peak', 'boardings', 'share']}
        data={rows}
        rowId={(r) => String(r.tripId)}
        sorting={[{ id: 'peak', desc: true }]}
        empty={t('dwell.noMatch')}
      />
    </section>
  )
}
