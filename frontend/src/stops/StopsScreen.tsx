import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { createColumnHelper } from '@tanstack/react-table'
import type { TFunction } from 'i18next'
import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { Period, StopStat, StopStatistics, StopStatisticsDetail, StopTrips } from '../api'
import { ChartFigure } from '../charts/ChartFigure'
import { useFormat, type Format } from '../i18n/format'
import { stopLabel } from '../map/directions'
import { numberParam, useScreenParams } from '../navigation'
import { ColumnChart } from '../operations/BarCharts'
import { FILTER_BAR, usePeriod, useReportPeriod } from '../operations/period'
import { ScreenLink } from '../operations/ScreenLink'
import { lineOptionMatch, matchesRow, weekdayNames } from '../operations/shared'
import { SortableTable } from '../operations/SortableTable'
import { sortableFeatures } from '../operations/tableFeatures'
import { DayKindSelect } from '../operations/TimeView'
import { HealthSkeleton } from '../quality/HealthSkeleton'
import { stopStatisticsDetailQuery, stopStatisticsQuery } from '../queries'
import { downloadCsv, type CsvColumn } from '../ui/csv'
import { DateRangePicker } from '../ui/DateRangePicker'
import { Empty } from '../ui/Empty'
import { LinkButton } from '../ui/LinkButton'
import { QueryState } from '../ui/QueryState'
import { SearchInput } from '../ui/SearchInput'
import { SearchSelect } from '../ui/SearchSelect'
import { Select } from '../ui/Select'
import { PairedBarChart } from './PairedBarChart'

const ALL = 'all'
const TRIP_KINDS: StopTrips[] = ['valid', 'all']
const BUTTON = 'inline-flex h-8 cursor-pointer items-center touch-target rounded-lg border border-rule bg-paper px-3 text-sm text-ink hover:border-ink-2'
const ICON_BUTTON =
  'inline-flex size-7 cursor-pointer items-center justify-center touch-target rounded-md border border-rule bg-paper text-ink-2 hover:border-ink-2 hover:text-ink disabled:cursor-default disabled:opacity-40'
const CLOSE =
  'inline-flex size-8 shrink-0 cursor-pointer items-center justify-center touch-target rounded-lg border border-rule bg-paper text-ink-2 hover:border-ink-2 hover:text-ink'

/** Which trips the figures count, narrowed by line, vehicle and period. */
interface StopQuery {
  line: number | null
  vehicle: number | null
  trips: StopTrips
  period: Period
}

/**
 * Stops as ADA's "Zastávky" summed them: per stop post, boarded, alighted and on board, narrowed by line,
 * vehicle, period and which trips count. Stops chosen in the table are compared in one chart, in the order
 * chosen; a stop opens by line, hour and weekday. "#/zastavky?stop=165902" opens on that stop.
 */
export function StopsScreen() {
  const { t } = useTranslation()
  const params = useScreenParams()
  const [line, setLine] = useState<number | null>(() => numberParam(params, 'line'))
  const [vehicle, setVehicle] = useState<number | null>(() => numberParam(params, 'vehicle'))
  const [trips, setTrips] = useState<StopTrips>('valid')
  const [selected, setSelected] = useState<number | null>(() => numberParam(params, 'stop'))
  const [chosen, setChosen] = useState<number[]>([])
  const { period, isAll } = useReportPeriod()
  const report = useQuery({ ...stopStatisticsQuery(line, vehicle, trips, period), placeholderData: keepPreviousData })
  return (
    <QueryState query={report} loading={t('stops.loading')} skeleton={<HealthSkeleton label={t('stops.loading')} />}>
      {(data) =>
        data.stops.length === 0 && line === null && vehicle === null && trips === 'all' && isAll ? (
          <Empty>{t('stops.empty')}</Empty>
        ) : (
          <StopsView
            report={data}
            query={{ line, vehicle, trips, period }}
            selected={selected}
            chosen={chosen}
            onLineChange={setLine}
            onVehicleChange={setVehicle}
            onTripsChange={setTrips}
            onSelect={setSelected}
            onChosenChange={setChosen}
          />
        )
      }
    </QueryState>
  )
}

function StopsView({
  report,
  query,
  selected,
  chosen,
  onLineChange,
  onVehicleChange,
  onTripsChange,
  onSelect,
  onChosenChange,
}: {
  report: StopStatistics
  query: StopQuery
  selected: number | null
  chosen: number[]
  onLineChange: (line: number | null) => void
  onVehicleChange: (vehicle: number | null) => void
  onTripsChange: (trips: StopTrips) => void
  onSelect: (code: number | null) => void
  onChosenChange: (chosen: number[]) => void
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

  const toggle = useCallback((code: number) => onChosenChange(chosen.includes(code) ? chosen.filter((c) => c !== code) : [...chosen, code]), [chosen, onChosenChange])
  const byCode = useMemo(() => new Map(report.stops.map((s) => [s.code, s])), [report.stops])
  const lineOptions = [{ value: ALL, label: t('dwell.allLines') }, ...report.lines.map((l) => ({ value: String(l), label: t('dwell.lineN', { line: l }) }))]
  const vehicleOptions = [{ value: ALL, label: t('trips.allVehicles') }, ...report.vehicles.map((v) => ({ value: String(v), label: t('trips.vehicleN', { vehicle: v }) }))]
  const boardings = report.stops.reduce((sum, s) => sum + s.boardings, 0)
  const selectedStop = selected === null ? undefined : byCode.get(selected)

  return (
    <div className="flex-1 overflow-y-auto p-4 md:px-7 md:pt-6 md:pb-10">
      <header>
        <h1 className="mb-2 text-2xl">{t('stops.title')}</h1>
        <dl className="flex flex-wrap gap-x-7 gap-y-1.5 [&>div]:flex [&>div]:items-baseline [&>div]:gap-2 [&_dd]:font-semibold [&_dt]:text-ink-2">
          <div>
            <dt>{t('stops.facts.period')}</dt>
            <dd>
              {report.from ? format.date(report.from) : '?'} – {report.to ? format.date(report.to) : '?'}
            </dd>
          </div>
          <div>
            <dt>{t('stops.facts.stops')}</dt>
            <dd>{format.number(report.stops.length)}</dd>
          </div>
          <div>
            <dt>{t('stops.facts.trips')}</dt>
            <dd>{format.number(report.trips)}</dd>
          </div>
          <div>
            <dt>{t('stops.facts.boardings')}</dt>
            <dd>{format.number(boardings)}</dd>
          </div>
        </dl>
        <p className="mt-2 max-w-[80ch] text-sm text-ink-2">{t('stops.note')}</p>
      </header>

      <div className={FILTER_BAR} role="group" aria-label={t('dates.filters')}>
        <SearchSelect
          label={t('dwell.line')}
          value={query.line === null ? ALL : String(query.line)}
          options={lineOptions}
          placeholder={t('dwell.searchLine')}
          empty={t('dwell.noOption')}
          match={lineOptionMatch}
          onChange={(v) => onLineChange(v === ALL ? null : Number(v))}
        />
        <SearchSelect
          label={t('trips.vehicle')}
          value={query.vehicle === null ? ALL : String(query.vehicle)}
          options={vehicleOptions}
          placeholder={t('trips.searchVehicle')}
          empty={t('dwell.noOption')}
          onChange={(v) => onVehicleChange(v === ALL ? null : Number(v))}
        />
        <DateRangePicker label={t('dates.period')} days={report.days} value={range} onChange={setRange} format={format} />
        <DayKindSelect />
        <Select label={t('stops.trips')} value={query.trips} options={TRIP_KINDS.map((k) => ({ value: k, label: t(`stops.tripKinds.${k}`) }))} onChange={(v) => onTripsChange(v as StopTrips)} />
      </div>

      {chosen.length > 0 && <ChosenStops stops={chosen.map((c) => byCode.get(c)).filter((s): s is StopStat => s !== undefined)} format={format} onChange={onChosenChange} />}

      {selected !== null && (
        <section ref={panel} tabIndex={-1} aria-labelledby="stop-detail" className="mb-8 rounded-[10px] border border-route bg-route-soft/40 p-4 outline-none md:p-5">
          <div className="mb-3 flex items-start justify-between gap-4">
            <h2 id="stop-detail" className="text-xl">
              {selectedStop ? stopLabel(selectedStop) : selected}
              <ScreenLink screen="provoz" params={{ stop: selected }} label={t('stops.detail.dwell')} title={t('links.dwellTitle', { name: selectedStop?.name ?? String(selected) })} />
            </h2>
            <button className={CLOSE} onClick={() => onSelect(null)} aria-label={t('stops.detail.close')}>
              <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden="true">
                <path d="M6 6l12 12M18 6 6 18" />
              </svg>
            </button>
          </div>
          <StopDetailPanel code={selected} query={query} format={format} />
        </section>
      )}

      <StopTable stops={report.stops} selected={selected} chosen={chosen} format={format} onOpen={onSelect} onToggle={toggle} />
    </div>
  )
}

function stopCsvColumns(t: TFunction): CsvColumn<StopStat>[] {
  return [
    { header: t('stops.columns.code'), value: (s) => s.code },
    { header: t('stops.columns.name'), value: (s) => stopLabel(s) },
    { header: t('stops.columns.lines'), value: (s) => s.lines.join(' ') },
    { header: t('stops.columns.visits'), value: (s) => s.visits },
    { header: t('stops.columns.passThroughs'), value: (s) => s.passThroughs },
    { header: t('stops.columns.boardings'), value: (s) => s.boardings },
    { header: t('stops.columns.alightings'), value: (s) => s.alightings },
    { header: t('stops.columns.meanLoad'), value: (s) => s.meanLoad },
    { header: t('stops.columns.maxLoad'), value: (s) => s.maxLoad },
  ]
}

const stopCol = createColumnHelper<typeof sortableFeatures, StopStat>()
function StopTable({
  stops,
  selected,
  chosen,
  format,
  onOpen,
  onToggle,
}: {
  stops: StopStat[]
  selected: number | null
  chosen: number[]
  format: Format
  onOpen: (code: number) => void
  onToggle: (code: number) => void
}) {
  const { t, i18n } = useTranslation()
  const [search, setSearch] = useState('')
  const rows = useMemo(() => stops.filter((s) => matchesRow(search, { exact: s.lines, prefix: [s.code], texts: [s.name, s.toward] })), [stops, search])
  const columns = useMemo(
    () => [
      stopCol.accessor('code', { id: 'code', header: t('stops.columns.code'), cell: (info) => <span className="tabular-nums">{info.getValue()}</span> }),
      stopCol.accessor((s) => stopLabel(s), {
        id: 'name',
        header: t('stops.columns.name'),
        cell: (info) => (
          <LinkButton className="text-left" onClick={() => onOpen(info.row.original.code)} aria-label={t('stops.open', { name: info.getValue() })}>
            {info.getValue()}
          </LinkButton>
        ),
      }),
      stopCol.accessor((s) => s.lines.join(', '), { id: 'lines', header: t('stops.columns.lines'), cell: (info) => <span className="text-ink-2">{info.getValue() || '–'}</span> }),
      stopCol.accessor('visits', { id: 'visits', header: t('stops.columns.visits'), cell: (info) => format.number(info.getValue()) }),
      stopCol.accessor('passThroughs', { id: 'passThroughs', header: t('stops.columns.passThroughs'), cell: (info) => format.number(info.getValue()) }),
      stopCol.accessor('boardings', { id: 'boardings', header: t('stops.columns.boardings'), cell: (info) => format.number(info.getValue()) }),
      stopCol.accessor('alightings', { id: 'alightings', header: t('stops.columns.alightings'), cell: (info) => format.number(info.getValue()) }),
      stopCol.accessor('meanLoad', { id: 'meanLoad', header: t('stops.columns.meanLoad'), cell: (info) => format.decimal(info.getValue()) }),
      stopCol.accessor('maxLoad', { id: 'maxLoad', header: t('stops.columns.maxLoad'), cell: (info) => format.number(info.getValue()) }),
      stopCol.display({
        id: 'choose',
        header: t('stops.columns.choose'),
        cell: (info) => {
          const s = info.row.original
          const isChosen = chosen.includes(s.code)
          return (
            <button
              className={ICON_BUTTON}
              aria-pressed={isChosen}
              onClick={() => onToggle(s.code)}
              aria-label={t(isChosen ? 'stops.unchoose' : 'stops.choose', { name: stopLabel(s) })}
            >
              {isChosen ? '✓' : '+'}
            </button>
          )
        },
      }),
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps -- labels change with the language
    [i18n.resolvedLanguage, format, onOpen, onToggle, chosen],
  )
  return (
    <section aria-labelledby="stops-list">
      <h2 id="stops-list" className="sr-only">
        {t('stops.title')}
      </h2>
      <div role="search" className="mt-3 mb-2.5 flex flex-wrap items-center gap-3">
        <SearchInput label={t('dwell.search')} placeholder={t('stops.search')} value={search} onChange={setSearch} />
        <span className="text-xs text-ink-2">{t('dwell.list.shown', { shown: format.number(rows.length), total: format.number(stops.length) })}</span>
        <button className={`${BUTTON} ml-auto`} onClick={() => downloadCsv(`${t('stops.exportFile')}.csv`, stopCsvColumns(t), rows)}>
          {t('stops.export')}
        </button>
      </div>
      <SortableTable
        columns={columns}
        numeric={['code', 'visits', 'passThroughs', 'boardings', 'alightings', 'meanLoad', 'maxLoad']}
        data={rows}
        rowId={(s) => String(s.code)}
        sorting={[{ id: 'boardings', desc: true }]}
        highlight={(s) => s.code === selected || chosen.includes(s.code)}
        empty={t('dwell.noMatch')}
      />
    </section>
  )
}

/** ADA's "Vybrané zastávky" and "Vytvořit graf": the chosen stops in the order chosen, compared in one chart. */
function ChosenStops({ stops, format, onChange }: { stops: StopStat[]; format: Format; onChange: (chosen: number[]) => void }) {
  const { t } = useTranslation()
  const codes = stops.map((s) => s.code)
  const move = (i: number, by: number) => {
    const next = [...codes]
    ;[next[i], next[i + by]] = [next[i + by], next[i]]
    onChange(next)
  }
  return (
    <div className="mb-8 grid gap-3">
      <ChartFigure
        title={t('stops.chosen.title')}
        subtitle={t('stops.chosen.subtitle')}
        controls={
          <>
            <button className={BUTTON} onClick={() => downloadCsv(`${t('stops.chosen.exportFile')}.csv`, stopCsvColumns(t), stops)}>
              {t('stops.export')}
            </button>
            <button className={BUTTON} onClick={() => onChange([])}>
              {t('stops.chosen.clear')}
            </button>
          </>
        }
        chart={
          <PairedBarChart
            rows={stops.map((s) => ({ key: s.code, label: s.name || String(s.code), sublabel: s.toward ? t('stops.chosen.toward', { toward: s.toward }) : null, description: stopLabel(s), first: s.boardings, second: s.alightings }))}
            keys={t('stops.chosen.keys')}
            names={[t('stops.columns.boardings'), t('stops.columns.alightings')]}
            formatValue={format.number}
          />
        }
        table={
          <SimpleTable
            headers={[t('stops.columns.name'), t('stops.columns.boardings'), t('stops.columns.alightings'), t('stops.columns.meanLoad')]}
            rows={stops.map((s) => [stopLabel(s), format.number(s.boardings), format.number(s.alightings), format.decimal(s.meanLoad)])}
          />
        }
      />
      <ol className="m-0 grid list-none gap-1.5 p-0 text-sm" aria-label={t('stops.chosen.title')}>
        {stops.map((s, i) => (
          <li key={s.code} className="flex flex-wrap items-center gap-2 rounded-lg border border-rule bg-paper px-2.5 py-1.5">
            <span className="w-6 text-ink-2 tabular-nums">{i + 1}.</span>
            <span className="min-w-[12rem] flex-1 font-semibold">{stopLabel(s)}</span>
            <span className="flex gap-1">
              <button className={ICON_BUTTON} disabled={i === 0} onClick={() => move(i, -1)} aria-label={t('stops.chosen.up', { name: stopLabel(s) })}>
                ↑
              </button>
              <button className={ICON_BUTTON} disabled={i === stops.length - 1} onClick={() => move(i, 1)} aria-label={t('stops.chosen.down', { name: stopLabel(s) })}>
                ↓
              </button>
              <button className={ICON_BUTTON} onClick={() => onChange(codes.filter((c) => c !== s.code))} aria-label={t('stops.chosen.remove', { name: stopLabel(s) })}>
                ✕
              </button>
            </span>
          </li>
        ))}
      </ol>
    </div>
  )
}

/** One stop post by line, by hour of arrival and by day of the week. */
function StopDetailPanel({ code, query, format }: { code: number; query: StopQuery; format: Format }) {
  const { t, i18n } = useTranslation()
  const detail = useQuery({ ...stopStatisticsDetailQuery(code, query.line, query.vehicle, query.trips, query.period), placeholderData: keepPreviousData })
  const longDay = weekdayNames(i18n.resolvedLanguage, 'long')
  return (
    <QueryState query={detail} loading={t('stops.loading')}>
      {(data: StopStatisticsDetail) => (
        <div className="grid gap-5 xl:grid-cols-2">
          <ChartFigure
            title={t('stops.detail.byHour.title')}
            subtitle={t('stops.detail.byHour.subtitle')}
            chart={
              <ColumnChart
                keys={t('stops.detail.byHour.keys')}
                formatValue={format.number}
                columns={data.byHour.map((h) => ({
                  key: h.hour,
                  label: String(h.hour),
                  value: h.boardings,
                  description: t('stops.detail.byHour.cell', { hour: h.hour, boardings: format.number(h.boardings), alightings: format.number(h.alightings) }),
                }))}
              />
            }
            table={
              <SimpleTable
                headers={[t('stops.detail.byHour.hour'), t('stops.columns.visits'), t('stops.columns.boardings'), t('stops.columns.alightings')]}
                rows={data.byHour.map((h) => [`${h.hour}:00`, format.number(h.visits), format.number(h.boardings), format.number(h.alightings)])}
              />
            }
          />
          <div className="grid content-start gap-5">
            <section>
              <h3 className="mb-2 text-lg">{t('stops.detail.byLine')}</h3>
              <SimpleTable
                headers={[t('stops.detail.line'), t('stops.columns.visits'), t('stops.columns.boardings'), t('stops.columns.alightings'), t('stops.columns.meanLoad')]}
                rows={data.byLine.map((l) => [
                  l.line === null ? t('stops.detail.noLine') : String(l.line),
                  format.number(l.visits),
                  format.number(l.boardings),
                  format.number(l.alightings),
                  format.decimal(l.meanLoad),
                ])}
              />
            </section>
            <section>
              <h3 className="mb-2 text-lg">{t('stops.detail.byWeekday')}</h3>
              <SimpleTable
                headers={[t('stops.detail.weekday'), t('stops.detail.days'), t('stops.columns.boardings'), t('stops.columns.alightings')]}
                rows={data.byWeekday.map((w) => [longDay[w.weekday - 1], String(w.days), format.number(Math.round(w.boardings / w.days)), format.number(Math.round(w.alightings / w.days))])}
              />
            </section>
          </div>
        </div>
      )}
    </QueryState>
  )
}

/** A small read-only table: the first column text, the rest numbers. */
function SimpleTable({ headers, rows }: { headers: string[]; rows: string[][] }) {
  return (
    <div className="overflow-x-auto rounded-[10px] border border-rule bg-paper">
      <table className="w-full border-collapse text-sm">
        <thead>
          <tr>
            {headers.map((h, i) => (
              <th key={h} className={`border-b border-rule px-3 py-2 font-semibold text-ink-2 ${i === 0 ? 'text-left' : 'text-right'}`}>
                {h}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={row[0]}>
              {row.map((cell, i) => (
                <td key={i} className={`border-b border-rule px-3 py-1.5 last:border-b-0 ${i === 0 ? 'text-left' : 'text-right tabular-nums'}`}>
                  {cell}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
