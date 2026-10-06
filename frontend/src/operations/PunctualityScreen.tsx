import type { TFunction } from 'i18next'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { createColumnHelper } from '@tanstack/react-table'
import { useMemo, useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import type { PunctualityReport, PunctualityRules, PunctualityStop, PunctualitySummary, TimesSource } from '../api'
import { ChartFigure } from '../charts/ChartFigure'
import { useFormat, type Format } from '../i18n/format'
import { stopLabel } from '../map/directions'
import { mapConfigQuery, punctualityQuery } from '../queries'
import { HealthSkeleton } from '../quality/HealthSkeleton'
import { Empty } from '../ui/Empty'
import { QueryState } from '../ui/QueryState'
import { SearchInput } from '../ui/SearchInput'
import { DateRangePicker } from '../ui/DateRangePicker'
import { SearchSelect } from '../ui/SearchSelect'
import { FILTER_BAR, usePeriod, useReportPeriod } from './period'
import { DayKindSelect, TimeViewSwitch, type TimeView } from './TimeView'
import { WeekHourHeatmap } from './WeekHourHeatmap'
import { NUM, TABLE, TD_COMPACT, TH_COMPACT } from '../ui/table'
import { cn } from '../ui/cn'
import { StackedShareChart, type Segment } from './BarCharts'
import { lineOptionMatch, matches, matchesRow, weekdayNames } from './shared'
import { SortableTable } from './SortableTable'
import { StopValueMap } from './StopValueMap'
import { sortableFeatures } from './tableFeatures'
import { FlagLegend, StepSwatch, type RowFlag } from './RowFlag'
import { Findings, type Finding } from './Findings'
import { numberParam, useScreenParams } from '../navigation'
import { ScreenLink } from './ScreenLink'
import { Select } from '../ui/Select'

const ALL = 'all'
/** Plot height of a chart beside a stop map, so the two cards line up. */
const MAP_SIDE_HEIGHT = 380
// Later = darker, on the one sequential ramp (these are shares, not health verdicts).
const FILL = { Early: 'fill-seq-1', OnTime: 'fill-seq-2', Late: 'fill-seq-4', VeryLate: 'fill-seq-6' } as const
const SWATCH = { Early: 'bg-seq-1', OnTime: 'bg-seq-2', Late: 'bg-seq-4', VeryLate: 'bg-seq-6' } as const
// Share of departures late → one of five map steps.
const LATE_BINS = [0.05, 0.1, 0.2, 0.35]
/** Departures a line or hour needs before it can be named the worst: a handful of trips proves nothing. */
const FINDING_MIN_DEPARTURES = 100
/** Late shares from which a line or stop is flagged: the map's two darkest steps. */
const LATE_WARNING = LATE_BINS[2]
const LATE_FAULT = LATE_BINS[3]
const lateShare = (s: PunctualitySummary) => (s.departures === 0 ? 0 : (s.late + s.veryLate) / s.departures)
const lateStep = (share: number) => {
  const i = LATE_BINS.findIndex((upper) => share <= upper)
  return (i === -1 ? LATE_BINS.length : i) + 1
}

/** Punctuality of departures, and how many passengers were on board when vehicles left late. */
export function PunctualityScreen() {
  const { t } = useTranslation()
  const params = useScreenParams()
  const [line, setLine] = useState<number | null>(() => numberParam(params, 'line'))
  const [times, setTimes] = useState<TimesSource>('vehicleLog')
  const { period, isAll } = useReportPeriod()
  const report = useQuery({ ...punctualityQuery(line, period, times), placeholderData: keepPreviousData })
  const mapConfig = useQuery(mapConfigQuery)
  return (
    <QueryState query={report} loading={t('punctuality.loading')} skeleton={<HealthSkeleton label={t('punctuality.loading')} />}>
      {(data) =>
        data.total.departures === 0 && line === null && isAll ? (
          <Empty>{t('punctuality.empty')}</Empty>
        ) : (
          <PunctualityView report={data} layers={mapConfig.data?.baseLayers} line={line} onLineChange={setLine} times={times} onTimesChange={setTimes} />
        )
      }
    </QueryState>
  )
}

function PunctualityView({
  report,
  layers,
  line,
  onLineChange,
  times,
  onTimesChange,
}: {
  report: PunctualityReport
  layers: import('../api').BaseLayer[] | undefined
  line: number | null
  onLineChange: (line: number | null) => void
  times: TimesSource
  onTimesChange: (times: TimesSource) => void
}) {
  const { t, i18n } = useTranslation()
  const format = useFormat()
  const [period, setPeriod] = usePeriod()
  const [timeView, setTimeView] = useState<TimeView>('hour')
  const shortDay = weekdayNames(i18n.resolvedLanguage)
  const longDay = weekdayNames(i18n.resolvedLanguage, 'long')
  const { total, rules } = report
  const share = (n: number, of: number) => (of === 0 ? '–' : format.percentWhole(n / of))
  // The answer first: how punctual overall, the worst line and hour, and where delays hit most passengers.
  const worst = <T,>(items: T[], score: (item: T) => number, enough: (item: T) => boolean) =>
    items.filter(enough).reduce<T | null>((w, item) => (w === null || score(item) > score(w) ? item : w), null)
  const worstLine = line === null ? worst(report.byLine, (l) => lateShare(l.summary), (l) => l.summary.departures >= FINDING_MIN_DEPARTURES) : null
  const worstHour = worst(report.hours, (h) => lateShare(h.summary), (h) => h.summary.departures >= FINDING_MIN_DEPARTURES)
  // With passengers, the stop where delays held up most people; without, the stop left late most often.
  const worstStop = report.hasPassengers
    ? worst(report.stops, (s) => s.summary.passengerMinutesLate, (s) => s.summary.passengerMinutesLate > 0)
    : worst(report.stops, (s) => lateShare(s.summary), (s) => s.summary.departures >= FINDING_MIN_DEPARTURES)
  const findings: Finding[] = [
    ...(total.departures > 0
      ? [{
          key: 'overall',
          text:
            total.passengersOnTimeShare === null
              ? t('findings.punctOverall', { onTime: share(total.onTime, total.departures) })
              : t('findings.punctOverallPassengers', { onTime: share(total.onTime, total.departures), passengers: format.percentWhole(total.passengersOnTimeShare) }),
        }]
      : []),
    ...(worstLine
      ? [{
          key: 'line',
          text: t('findings.punctWorstLine', { line: worstLine.line, late: format.percentWhole(lateShare(worstLine.summary)) }),
          action: { label: t('findings.showLine'), onClick: () => onLineChange(worstLine.line) },
        }]
      : []),
    ...(worstHour && lateShare(worstHour.summary) > 0 ? [{ key: 'hour', text: t('findings.punctWorstHour', { hour: worstHour.hour, late: format.percentWhole(lateShare(worstHour.summary)) }) }] : []),
    ...(worstStop
      ? [{
          key: 'stop',
          text: report.hasPassengers
            ? t('findings.punctWorstStop', { stop: stopLabel(worstStop), minutes: format.number(worstStop.summary.passengerMinutesLate) })
            : t('findings.punctWorstStopLate', { stop: stopLabel(worstStop), late: format.percentWhole(lateShare(worstStop.summary)) }),
        }]
      : []),
  ]
  const limits = { early: format.seconds(rules.earlySeconds), late: format.seconds(rules.lateSeconds), veryLate: format.seconds(rules.veryLateSeconds) }
  const segments = (s: PunctualitySummary): Segment[] =>
    (['Early', 'OnTime', 'Late', 'VeryLate'] as const).map((k) => ({
      label: t(`punctuality.categoryRanges.${k}`, limits),
      value: k === 'Early' ? s.early : k === 'OnTime' ? s.onTime : k === 'Late' ? s.late : s.veryLate,
      fill: FILL[k],
      swatch: SWATCH[k],
    }))
  const timeColumns =
    timeView === 'weekday'
      ? report.weekdays.map((w) => ({ key: w.weekday, label: shortDay[w.weekday - 1], name: longDay[w.weekday - 1], days: w.days, segments: segments(w.summary), summary: w.summary }))
      : report.hours.map((h) => ({ key: h.hour, label: String(h.hour), name: '', days: 0, segments: segments(h.summary), summary: h.summary }))
  const lineOptions = [{ value: ALL, label: t('dwell.allLines') }, ...report.lines.map((l) => ({ value: String(l), label: t('dwell.lineN', { line: l }) }))]
  const mapStops = useMemo(
    () =>
      report.stops.map((s) => ({
        ...s,
        size: s.summary.departures,
        step: lateStep(lateShare(s.summary)),
        detail: `${t('punctuality.columns.late')} ${format.percentWhole(lateShare(s.summary))} · ${t('punctuality.columns.median')} ${signed(s.summary.medianDelaySeconds, format)} · ${format.number(s.summary.departures)} ${t('punctuality.columns.departures').toLowerCase()}`,
      })),
    // eslint-disable-next-line react-hooks/exhaustive-deps -- labels change with the language
    [report.stops, format, i18n.resolvedLanguage],
  )
  const legend = [`≤ ${format.percentWhole(LATE_BINS[0])}`, ...LATE_BINS.slice(1).map((upper, i) => `${format.percentWhole(LATE_BINS[i])} – ${format.percentWhole(upper)}`), `> ${format.percentWhole(LATE_BINS[LATE_BINS.length - 1])}`]

  return (
    <div className="flex-1 overflow-y-auto p-4 md:px-7 md:pt-6 md:pb-10">
      <header>
        <h1 className="mb-2 text-2xl">{t('punctuality.title')}</h1>
        <dl className="flex flex-wrap gap-x-7 gap-y-1.5 [&>div]:flex [&>div]:items-baseline [&>div]:gap-2 [&_dd]:font-semibold [&_dt]:text-ink-2">
          <div>
            <dt>{t('punctuality.facts.period')}</dt>
            <dd>
              {report.from ? format.date(report.from) : '?'} – {report.to ? format.date(report.to) : '?'}
            </dd>
          </div>
          <div>
            <dt>{t('punctuality.facts.departures')}</dt>
            <dd>{format.number(total.departures)}</dd>
          </div>
          <div>
            <dt>{t('punctuality.facts.onTime')}</dt>
            <dd>{share(total.onTime, total.departures)}</dd>
          </div>
          {report.hasPassengers && (
            <>
              <div>
                <dt>{t('punctuality.facts.passengersOnTime')}</dt>
                <dd>{total.passengersOnTimeShare === null ? '–' : format.percentWhole(total.passengersOnTimeShare)}</dd>
              </div>
              <div>
                <dt>{t('punctuality.facts.passengerMinutes')}</dt>
                <dd>{format.number(total.passengerMinutesLate)}</dd>
              </div>
            </>
          )}
        </dl>
        <Findings
          items={findings}
          method={
            <>
              <p className="m-0">{t('punctuality.summary', { early: format.seconds(rules.earlySeconds), late: format.seconds(rules.lateSeconds), veryLate: format.seconds(rules.veryLateSeconds) })}</p>
              <p className="m-0">{t('punctuality.weighting')}</p>
            </>
          }
        />
      </header>

      <div className={FILTER_BAR} role="group" aria-label={t('dates.filters')}>
        <Select
          label={t('punctuality.source.label')}
          value={times}
          options={[
            { value: 'vehicleLog', label: t('punctuality.source.vehicleLog') },
            { value: 'transportella', label: t('punctuality.source.transportella') },
          ]}
          onChange={(v) => onTimesChange(v as TimesSource)}
        />
        <SearchSelect
          label={t('dwell.line')}
          value={line === null ? ALL : String(line)}
          options={lineOptions}
          placeholder={t('dwell.searchLine')}
          empty={t('dwell.noOption')}
          match={lineOptionMatch}
          onChange={(v) => onLineChange(v === ALL ? null : Number(v))}
        />
        <DateRangePicker label={t('dates.period')} days={report.days} value={period} onChange={setPeriod} format={format} />
        <DayKindSelect />
      </div>

      <div className="mb-6 grid gap-5 xl:grid-cols-2">
        {layers && (
          <StopValueMap
            layers={layers}
            stops={mapStops}
            title={t('punctuality.map.title')}
            subtitle={t('punctuality.map.subtitle', { late: format.seconds(rules.lateSeconds) })}
            legendTitle={t('punctuality.map.legend')}
            legendLabels={legend}
          />
        )}
        <ChartFigure
          title={t(`punctuality.${timeView}.title`)}
          subtitle={t(`punctuality.${timeView}.subtitle`)}
          controls={<TimeViewSwitch value={timeView} onChange={setTimeView} />}
          chart={
            timeView === 'week' ? (
              <WeekHourHeatmap
                keys={t('punctuality.week.keys')}
                height={MAP_SIDE_HEIGHT + 30}
                legend={legend}
                cells={report.weekHours.map((c) => ({
                  weekday: c.weekday,
                  hour: c.hour,
                  step: lateStep(lateShare(c.summary)) - 1,
                  value: format.percentWhole(lateShare(c.summary)),
                  label: t('punctuality.week.cell', { day: longDay[c.weekday - 1], hour: c.hour, late: format.percentWhole(lateShare(c.summary)), departures: format.number(c.summary.departures) }),
                }))}
              />
            ) : (
              <StackedShareChart
                columns={timeColumns}
                height={MAP_SIDE_HEIGHT}
                keys={t(`punctuality.${timeView}.keys`)}
                describe={(c) => {
                  const column = timeColumns.find((h) => h.label === c.label)!
                  const s = column.summary
                  return t(`punctuality.${timeView}.cell`, {
                    hour: c.label,
                    day: column.name,
                    days: column.days,
                    departures: format.number(s.departures),
                    onTime: share(s.onTime, s.departures),
                    late: share(s.late + s.veryLate, s.departures),
                    early: share(s.early, s.departures),
                  })
                }}
              />
            )
          }
          table={
            <SummaryTable
              rows={
                timeView === 'hour'
                  ? report.hours.map((h) => ({ key: h.hour, label: `${h.hour}:00`, summary: h.summary }))
                  : timeView === 'weekday'
                    ? report.weekdays.map((w) => ({ key: w.weekday, label: t('dates.weekdayDays', { day: longDay[w.weekday - 1], count: w.days }), summary: w.summary }))
                    : report.weekHours.map((c) => ({ key: c.weekday * 100 + c.hour, label: `${shortDay[c.weekday - 1]} ${c.hour}:00`, summary: c.summary }))
              }
              labelHeader={t(timeView === 'hour' ? 'punctuality.hours.hour' : 'dates.weekday')}
              format={format}
            />
          }
        />
      </div>

      {line === null && report.byLine.length > 0 && <LineTable lines={report.byLine} rules={rules} passengers={report.hasPassengers} format={format} />}

      <StopTable stops={report.stops} rules={rules} passengers={report.hasPassengers} format={format} />
    </div>
  )
}

const signed = (seconds: number, format: Format) => (seconds > 0 ? '+' : '') + format.seconds(seconds)

interface SummaryRow {
  key: number
  label: string
  sortLabel?: number | string
  summary: PunctualitySummary
}

const summaryCol = createColumnHelper<typeof sortableFeatures, SummaryRow>()
const SUMMARY_NUMERIC = ['departures', 'onTime', 'late', 'early', 'median', 'passengerMinutes', 'passengersOnTime']

function summaryColumns(
  t: TFunction,
  format: Format,
  labelHeader: string,
  rules: PunctualityRules,
  passengers: boolean,
  renderLabel?: (row: SummaryRow) => ReactNode,
) {
  const pct = (n: number, of: number) => (of === 0 ? 0 : n / of)
  const limits = { early: format.seconds(rules.earlySeconds), late: format.seconds(rules.lateSeconds) }
  const columns = [
    summaryCol.accessor((r) => r.sortLabel ?? r.label, { id: 'label', header: labelHeader, cell: (info) => (renderLabel ? renderLabel(info.row.original) : info.row.original.label) }),
    summaryCol.accessor((r) => r.summary.departures, { id: 'departures', header: t('punctuality.columns.departures'), cell: (info) => format.number(info.getValue()) }),
    summaryCol.accessor((r) => pct(r.summary.onTime, r.summary.departures), { id: 'onTime', header: t('punctuality.columns.onTimeRange', limits), cell: (info) => format.percentWhole(info.getValue()) }),
    summaryCol.accessor((r) => pct(r.summary.late + r.summary.veryLate, r.summary.departures), {
      id: 'late',
      header: t('punctuality.columns.lateRange', limits),
      cell: (info) => (
        <span className="font-semibold whitespace-nowrap">
          <StepSwatch step={lateStep(info.getValue())} />
          {format.percentWhole(info.getValue())}
        </span>
      ),
    }),
    summaryCol.accessor((r) => pct(r.summary.early, r.summary.departures), { id: 'early', header: t('punctuality.columns.earlyRange', limits), cell: (info) => format.percentWhole(info.getValue()) }),
    summaryCol.accessor((r) => r.summary.medianDelaySeconds, { id: 'median', header: t('punctuality.columns.median'), cell: (info) => signed(info.getValue(), format) }),
    summaryCol.accessor((r) => r.summary.passengerMinutesLate, { id: 'passengerMinutes', header: t('punctuality.columns.passengerMinutes'), cell: (info) => format.number(info.getValue()) }),
    summaryCol.accessor((r) => r.summary.passengersOnTimeShare ?? -1, {
      id: 'passengersOnTime',
      header: t('punctuality.columns.passengersOnTime'),
      cell: (info) => (info.row.original.summary.passengersOnTimeShare === null ? '–' : format.percentWhole(info.row.original.summary.passengersOnTimeShare)),
    }),
  ]
  return passengers ? columns : columns.filter((c) => c.id !== 'passengerMinutes' && c.id !== 'passengersOnTime')
}

/** Tables start with the most passenger-minutes late, or without passengers, the largest share late. */
const summarySorting = (passengers: boolean) => [{ id: passengers ? 'passengerMinutes' : 'late', desc: true }]

function SummarySortable({ rows, labelHeader, rules, passengers, format }: { rows: SummaryRow[]; labelHeader: string; rules: PunctualityRules; passengers: boolean; format: Format }) {
  const { t, i18n } = useTranslation()
  // eslint-disable-next-line react-hooks/exhaustive-deps -- labels change with the language
  // Each line links to its occupancy.
  const columns = useMemo(
    () =>
      summaryColumns(t, format, labelHeader, rules, passengers, (r) => (
        <>
          {r.label}
          <ScreenLink screen="obsazenost" params={{ line: r.key }} label={t('links.load')} title={t('links.loadTitle', { name: r.label })} />
        </>
      )),
    // eslint-disable-next-line react-hooks/exhaustive-deps -- labels change with the language
    [i18n.resolvedLanguage, format, labelHeader, rules, passengers],
  )
  return (
    <>
      <SortableTable key={String(passengers)} columns={columns} numeric={SUMMARY_NUMERIC} data={rows} rowId={(r) => String(r.key)} sorting={summarySorting(passengers)} flag={(r) => lateFlag(r.summary, t, format)} />
      <LateLegend format={format} />
    </>
  )
}

/** Flag a line or stop by its share of late departures: the map's two darkest steps. */
function lateFlag(s: PunctualitySummary, t: TFunction, format: Format): RowFlag | null {
  const share = lateShare(s)
  const late = format.percentWhole(share)
  return share > LATE_FAULT ? { status: 'Fault', reason: t('flags.lateFault', { late }) } : share > LATE_WARNING ? { status: 'Warning', reason: t('flags.lateWarning', { late }) } : null
}

function LateLegend({ format }: { format: Format }) {
  const { t } = useTranslation()
  return <FlagLegend warning={t('flags.lateLegendWarning', { from: format.percentWhole(LATE_WARNING) })} fault={t('flags.lateLegendFault', { from: format.percentWhole(LATE_FAULT) })} />
}

/** The table view behind the hour chart: plain rows in hour order. */
function SummaryTable({ rows, labelHeader, format }: { rows: SummaryRow[]; labelHeader: string; format: Format }) {
  const { t } = useTranslation()
  const pct = (n: number, of: number) => (of === 0 ? '–' : format.percentWhole(n / of))
  return (
    <table className={TABLE}>
      <thead>
        <tr>
          <th className={TH_COMPACT}>{labelHeader}</th>
          <th className={cn(TH_COMPACT, NUM)}>{t('punctuality.columns.departures')}</th>
          <th className={cn(TH_COMPACT, NUM)}>{t('punctuality.columns.onTime')}</th>
          <th className={cn(TH_COMPACT, NUM)}>{t('punctuality.columns.late')}</th>
          <th className={cn(TH_COMPACT, NUM)}>{t('punctuality.columns.early')}</th>
          <th className={cn(TH_COMPACT, NUM)}>{t('punctuality.columns.median')}</th>
        </tr>
      </thead>
      <tbody>
        {rows.map((r) => (
          <tr key={r.key}>
            <td className={TD_COMPACT}>{r.label}</td>
            <td className={cn(TD_COMPACT, NUM)}>{format.number(r.summary.departures)}</td>
            <td className={cn(TD_COMPACT, NUM)}>{pct(r.summary.onTime, r.summary.departures)}</td>
            <td className={cn(TD_COMPACT, NUM)}>{pct(r.summary.late + r.summary.veryLate, r.summary.departures)}</td>
            <td className={cn(TD_COMPACT, NUM)}>{pct(r.summary.early, r.summary.departures)}</td>
            <td className={cn(TD_COMPACT, NUM)}>{signed(r.summary.medianDelaySeconds, format)}</td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}

function LineTable({ lines, rules, passengers, format }: { lines: PunctualityReport['byLine']; rules: PunctualityRules; passengers: boolean; format: Format }) {
  const { t } = useTranslation()
  const [search, setSearch] = useState('')
  const rows = useMemo(
    () =>
      lines
        .filter((l) => matchesRow(search, { exact: [l.line], texts: [l.line] }))
        .map((l) => ({ key: l.line, label: t('dwell.lineN', { line: l.line }), sortLabel: l.line, summary: l.summary })),
    [lines, search, t],
  )
  return (
    <section className="mb-8" aria-labelledby="punctuality-lines">
      <div className="mb-2.5">
        <h2 id="punctuality-lines" className="text-xl">
          {t('punctuality.lines.title')}
        </h2>
        <div role="search" className="mt-3 flex flex-wrap items-center gap-3">
          <SearchInput label={t('dwell.search')} placeholder={t('punctuality.lines.search')} value={search} onChange={setSearch} />
          <span className="text-xs text-ink-2">{t('dwell.list.shown', { shown: format.number(rows.length), total: format.number(lines.length) })}</span>
        </div>
      </div>
      <SummarySortable rows={rows} labelHeader={t('punctuality.lines.line')} rules={rules} passengers={passengers} format={format} />
    </section>
  )
}

function StopTable({ stops, rules, passengers, format }: { stops: PunctualityStop[]; rules: PunctualityRules; passengers: boolean; format: Format }) {
  const minDepartures = rules.minDeparturesPerStop
  const { t, i18n } = useTranslation()
  const [search, setSearch] = useState('')
  const rows = useMemo(
    () =>
      stops
        .filter((s) => matches(search, s.name, s.code, s.toward))
        .map((s) => ({ key: s.code, label: stopLabel(s), summary: s.summary })),
    [stops, search],
  )
  // Each stop links to its dwell.
  const columns = useMemo(
    () =>
      summaryColumns(t, format, t('punctuality.stops.stop'), rules, passengers, (r) => (
        <>
          {r.label} <span className="text-xs text-ink-2 tabular-nums">{r.key}</span>
          <ScreenLink screen="provoz" params={{ stop: r.key }} label={t('links.dwell')} title={t('links.dwellTitle', { name: r.label })} />
        </>
      )),
    // eslint-disable-next-line react-hooks/exhaustive-deps -- labels change with the language
    [i18n.resolvedLanguage, format, rules, passengers],
  )

  return (
    <section aria-labelledby="punctuality-stops">
      <div className="mb-2.5">
        <div>
          <h2 id="punctuality-stops" className="text-xl">
            {t('punctuality.stops.title')}
          </h2>
          <p className="mt-0.5 text-sm text-ink-2">{t('punctuality.stops.subtitle', { min: minDepartures })}</p>
        </div>
        <div role="search" className="mt-3 flex flex-wrap items-center gap-3">
          <SearchInput label={t('dwell.search')} placeholder={t('dwell.searchStops')} value={search} onChange={setSearch} />
          <span className="text-xs text-ink-2">{t('dwell.list.shown', { shown: format.number(rows.length), total: format.number(stops.length) })}</span>
        </div>
      </div>
      <SortableTable
        key={String(passengers)}
        flag={(r) => lateFlag(r.summary, t, format)}
        columns={columns}
        numeric={SUMMARY_NUMERIC}
        data={rows}
        rowId={(r) => String(r.key)}
        sorting={summarySorting(passengers)}
        empty={t('dwell.noMatch')}
      />
      <LateLegend format={format} />
    </section>
  )
}
