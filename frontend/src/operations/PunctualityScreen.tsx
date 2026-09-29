import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { createColumnHelper } from '@tanstack/react-table'
import { useMemo, useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import type { PunctualityReport, PunctualityStop, PunctualitySummary } from '../api'
import { ChartFigure } from '../charts/ChartFigure'
import { useFormat, type Format } from '../i18n/format'
import { stopLabel } from '../map/directions'
import { mapConfigQuery, punctualityQuery } from '../queries'
import { HealthSkeleton } from '../quality/HealthSkeleton'
import { Empty } from '../ui/Empty'
import { QueryState } from '../ui/QueryState'
import { SearchInput } from '../ui/SearchInput'
import { Select } from '../ui/Select'
import { NUM, TABLE, TD_COMPACT, TH_COMPACT } from '../ui/table'
import { cn } from '../ui/cn'
import { StackedShareChart, type Segment } from './BarCharts'
import { matches } from './shared'
import { SortableTable } from './SortableTable'
import { StopValueMap } from './StopValueMap'
import { sortableFeatures } from './tableFeatures'

const ALL = 'all'
// Later = darker, on the one sequential ramp (these are shares, not health verdicts).
const FILL = { Early: 'fill-seq-1', OnTime: 'fill-seq-2', Late: 'fill-seq-4', VeryLate: 'fill-seq-6' } as const
const SWATCH = { Early: 'bg-seq-1', OnTime: 'bg-seq-2', Late: 'bg-seq-4', VeryLate: 'bg-seq-6' } as const
// Share of departures late → one of five map steps.
const LATE_BINS = [0.05, 0.1, 0.2, 0.35]
const lateShare = (s: PunctualitySummary) => (s.departures === 0 ? 0 : (s.late + s.veryLate) / s.departures)
const lateStep = (share: number) => {
  const i = LATE_BINS.findIndex((upper) => share <= upper)
  return (i === -1 ? LATE_BINS.length : i) + 1
}

/** Punctuality of departures, and how many passengers were on board when vehicles left late. */
export function PunctualityScreen() {
  const { t } = useTranslation()
  const [line, setLine] = useState<number | null>(null)
  const report = useQuery({ ...punctualityQuery(line), placeholderData: keepPreviousData })
  const mapConfig = useQuery(mapConfigQuery)
  return (
    <QueryState query={report} loading={t('punctuality.loading')} skeleton={<HealthSkeleton label={t('punctuality.loading')} />}>
      {(data) =>
        data.total.departures === 0 && line === null ? (
          <Empty>{t('punctuality.empty')}</Empty>
        ) : (
          <PunctualityView report={data} layers={mapConfig.data?.baseLayers} line={line} onLineChange={setLine} />
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
}: {
  report: PunctualityReport
  layers: import('../api').BaseLayer[] | undefined
  line: number | null
  onLineChange: (line: number | null) => void
}) {
  const { t, i18n } = useTranslation()
  const format = useFormat()
  const { total, rules } = report
  const share = (n: number, of: number) => (of === 0 ? '–' : format.percentWhole(n / of))
  const segments = (s: PunctualitySummary): Segment[] =>
    (['Early', 'OnTime', 'Late', 'VeryLate'] as const).map((k) => ({
      label: t(`punctuality.categories.${k}`),
      value: k === 'Early' ? s.early : k === 'OnTime' ? s.onTime : k === 'Late' ? s.late : s.veryLate,
      fill: FILL[k],
      swatch: SWATCH[k],
    }))
  const hourColumns = report.hours.map((h) => ({ key: h.hour, label: String(h.hour), segments: segments(h.summary), summary: h.summary }))
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
          <div>
            <dt>{t('punctuality.facts.passengersOnTime')}</dt>
            <dd>{total.passengersOnTimeShare === null ? '–' : format.percentWhole(total.passengersOnTimeShare)}</dd>
          </div>
          <div>
            <dt>{t('punctuality.facts.passengerMinutes')}</dt>
            <dd>{format.number(total.passengerMinutesLate)}</dd>
          </div>
        </dl>
        <p className="mt-3 max-w-[72ch] text-sm text-ink-2">
          {t('punctuality.summary', { early: format.seconds(rules.earlySeconds), late: format.seconds(rules.lateSeconds), veryLate: format.seconds(rules.veryLateSeconds) })}{' '}
          {t('punctuality.weighting')}
        </p>
      </header>

      <div className="my-5 flex flex-wrap items-center gap-3">
        <Select label={t('dwell.line')} value={line === null ? ALL : String(line)} options={lineOptions} onChange={(v) => onLineChange(v === ALL ? null : Number(v))} />
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
          title={t('punctuality.hours.title')}
          subtitle={t('punctuality.hours.subtitle')}
          chart={
            <StackedShareChart
              columns={hourColumns}
              keys={t('punctuality.hours.keys')}
              describe={(c) => {
                const s = hourColumns.find((h) => h.label === c.label)!.summary
                return t('punctuality.hours.cell', {
                  hour: c.label,
                  departures: format.number(s.departures),
                  onTime: share(s.onTime, s.departures),
                  late: share(s.late + s.veryLate, s.departures),
                  early: share(s.early, s.departures),
                })
              }}
            />
          }
          table={<SummaryTable rows={report.hours.map((h) => ({ key: h.hour, label: `${h.hour}:00`, summary: h.summary }))} labelHeader={t('punctuality.hours.hour')} format={format} />}
        />
      </div>

      {line === null && report.byLine.length > 0 && (
        <section className="mb-8" aria-labelledby="punctuality-lines">
          <h2 id="punctuality-lines" className="mb-2.5 text-xl">
            {t('punctuality.lines.title')}
          </h2>
          <SummarySortable rows={report.byLine.map((l) => ({ key: l.line, label: t('dwell.lineN', { line: l.line }), sortLabel: l.line, summary: l.summary }))} labelHeader={t('punctuality.lines.line')} format={format} />
        </section>
      )}

      <StopTable stops={report.stops} minDepartures={rules.minDeparturesPerStop} format={format} />
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

function summaryColumns(t: ReturnType<typeof useTranslation>['t'], format: Format, labelHeader: string, renderLabel?: (row: SummaryRow) => ReactNode) {
  const pct = (n: number, of: number) => (of === 0 ? 0 : n / of)
  return [
    summaryCol.accessor((r) => r.sortLabel ?? r.label, { id: 'label', header: labelHeader, cell: (info) => (renderLabel ? renderLabel(info.row.original) : info.row.original.label) }),
    summaryCol.accessor((r) => r.summary.departures, { id: 'departures', header: t('punctuality.columns.departures'), cell: (info) => format.number(info.getValue()) }),
    summaryCol.accessor((r) => pct(r.summary.onTime, r.summary.departures), { id: 'onTime', header: t('punctuality.columns.onTime'), cell: (info) => format.percentWhole(info.getValue()) }),
    summaryCol.accessor((r) => pct(r.summary.late + r.summary.veryLate, r.summary.departures), {
      id: 'late',
      header: t('punctuality.columns.late'),
      cell: (info) => <span className="font-semibold">{format.percentWhole(info.getValue())}</span>,
    }),
    summaryCol.accessor((r) => pct(r.summary.early, r.summary.departures), { id: 'early', header: t('punctuality.columns.early'), cell: (info) => format.percentWhole(info.getValue()) }),
    summaryCol.accessor((r) => r.summary.medianDelaySeconds, { id: 'median', header: t('punctuality.columns.median'), cell: (info) => signed(info.getValue(), format) }),
    summaryCol.accessor((r) => r.summary.passengerMinutesLate, { id: 'passengerMinutes', header: t('punctuality.columns.passengerMinutes'), cell: (info) => format.number(info.getValue()) }),
    summaryCol.accessor((r) => r.summary.passengersOnTimeShare ?? -1, {
      id: 'passengersOnTime',
      header: t('punctuality.columns.passengersOnTime'),
      cell: (info) => (info.row.original.summary.passengersOnTimeShare === null ? '–' : format.percentWhole(info.row.original.summary.passengersOnTimeShare)),
    }),
  ]
}

function SummarySortable({ rows, labelHeader, format }: { rows: SummaryRow[]; labelHeader: string; format: Format }) {
  const { t, i18n } = useTranslation()
  // eslint-disable-next-line react-hooks/exhaustive-deps -- labels change with the language
  const columns = useMemo(() => summaryColumns(t, format, labelHeader), [i18n.resolvedLanguage, format, labelHeader])
  return <SortableTable columns={columns} numeric={SUMMARY_NUMERIC} data={rows} rowId={(r) => String(r.key)} sorting={[{ id: 'passengerMinutes', desc: true }]} />
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

function StopTable({ stops, minDepartures, format }: { stops: PunctualityStop[]; minDepartures: number; format: Format }) {
  const { t, i18n } = useTranslation()
  const [search, setSearch] = useState('')
  const rows = useMemo(
    () =>
      stops
        .filter((s) => matches(search, s.name, s.code, s.toward))
        .map((s) => ({ key: s.code, label: stopLabel(s), summary: s.summary })),
    [stops, search],
  )
  // eslint-disable-next-line react-hooks/exhaustive-deps -- labels change with the language
  const columns = useMemo(() => summaryColumns(t, format, t('punctuality.stops.stop'), (r) => <>{r.label} <span className="text-xs text-ink-2 tabular-nums">{r.key}</span></>), [i18n.resolvedLanguage, format])

  return (
    <section aria-labelledby="punctuality-stops">
      <div className="mb-2.5 flex flex-wrap items-end justify-between gap-x-4 gap-y-2">
        <div>
          <h2 id="punctuality-stops" className="text-xl">
            {t('punctuality.stops.title')}
          </h2>
          <p className="mt-0.5 text-sm text-ink-2">{t('punctuality.stops.subtitle', { min: minDepartures })}</p>
        </div>
        <div role="search" className="flex items-center gap-3">
          <SearchInput label={t('dwell.search')} placeholder={t('dwell.searchStops')} value={search} onChange={setSearch} />
          <span className="text-xs text-ink-2">{t('dwell.list.shown', { shown: format.number(rows.length), total: format.number(stops.length) })}</span>
        </div>
      </div>
      <SortableTable columns={columns} numeric={SUMMARY_NUMERIC} data={rows} rowId={(r) => String(r.key)} sorting={[{ id: 'passengerMinutes', desc: true }]} empty={t('dwell.noMatch')} />
    </section>
  )
}
