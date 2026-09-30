import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { BaseLayer, DwellCause, DwellReport } from '../api'
import { ChartFigure } from '../charts/ChartFigure'
import { useFormat } from '../i18n/format'
import { dwellQuery, mapConfigQuery } from '../queries'
import { HealthSkeleton } from '../quality/HealthSkeleton'
import { Empty } from '../ui/Empty'
import { QueryState } from '../ui/QueryState'
import { DateRangePicker } from '../ui/DateRangePicker'
import { SearchSelect } from '../ui/SearchSelect'
import { FILTER_BAR, usePeriod, useReportPeriod } from './period'
import { DayKindSelect } from './TimeView'
import { DwellBandsChart, DwellBandsTable } from './DwellBandsChart'
import { DwellMap } from './DwellMap'
import { lineOptionMatch, stopLabel, type DwellDetail } from './shared'
import { StopDetail } from './StopDetail'
import { StopRanking } from './StopRanking'
import { UnexplainedList } from './UnexplainedList'
import { VehicleDay } from './VehicleDay'
import { Findings, type Finding } from './Findings'
import { numberParam, useScreenParams } from '../navigation'

const ALL = 'all'
/** Plot height of a chart beside a stop map, so the two cards line up. */
const MAP_SIDE_HEIGHT = 380

/**
 * Dwell at stops, from overview to case: where and how much vehicles stand longer than their passengers
 * need (map, chart), one stop or one vehicle's day in detail, and the tables to check the numbers.
 */
export function DwellScreen() {
  const { t } = useTranslation()
  const params = useScreenParams()
  const [line, setLine] = useState<number | null>(() => numberParam(params, 'line'))
  const { period, isAll } = useReportPeriod()
  // Switching lines or days keeps the previous report on screen until the next one arrives.
  const report = useQuery({ ...dwellQuery(line, period), placeholderData: keepPreviousData })
  const mapConfig = useQuery(mapConfigQuery)
  return (
    <QueryState query={report} loading={t('dwell.loading')} skeleton={<HealthSkeleton label={t('dwell.loading')} />}>
      {(data) =>
        data.model.visits === 0 && line === null && isAll ? (
          <Empty>{t('dwell.empty')}</Empty>
        ) : (
          <DwellView report={data} layers={mapConfig.data?.baseLayers} line={line} onLineChange={setLine} initialStop={numberParam(params, 'stop')} />
        )
      }
    </QueryState>
  )
}

function DwellView({
  report,
  layers,
  line,
  onLineChange,
  initialStop,
}: {
  report: DwellReport
  layers: BaseLayer[] | undefined
  line: number | null
  onLineChange: (line: number | null) => void
  /** A stop to open in detail straight away (a link from another screen). */
  initialStop: number | null
}) {
  const { t } = useTranslation()
  const format = useFormat()
  const { model, rules } = report
  const [detail, setDetail] = useState<DwellDetail | null>(() => (initialStop === null ? null : { kind: 'stop', code: initialStop }))
  const [period, setPeriod] = usePeriod()
  const { period: reportPeriod } = useReportPeriod()
  const panel = useRef<HTMLElement>(null)
  const lineOptions = useMemo(
    () => [{ value: ALL, label: t('dwell.allLines') }, ...report.lines.map((l) => ({ value: String(l), label: t('dwell.lineN', { line: l }) }))],
    [report.lines, t],
  )
  const stopName = (code: number) => stopLabel(report.stops.find((s) => s.code === code), code)

  const openStop = useCallback((code: number) => setDetail({ kind: 'stop', code }), [])
  const openVehicle = useCallback((vehicle: number, day: string, at?: string) => setDetail({ kind: 'vehicle', vehicle, day, at }), [])

  // The answer first: the stop that loses most time, what the long dwells were, and what a passenger costs.
  const worstStop = report.stops.reduce<(typeof report.stops)[number] | null>((worst, s) => (s.medianExcessSeconds > (worst?.medianExcessSeconds ?? 0) ? s : worst), null)
  const causeCounts = report.unexplained.reduce<Record<string, number>>((counts, r) => ({ ...counts, [r.cause]: (counts[r.cause] ?? 0) + 1 }), {})
  const topCause = Object.entries(causeCounts).sort((a, b) => b[1] - a[1])[0] as [DwellCause, number] | undefined
  const findings: Finding[] = [
    ...(worstStop
      ? [{
          key: 'stop',
          text: t('findings.dwellWorstStop', { stop: stopLabel(worstStop), excess: format.seconds(worstStop.medianExcessSeconds), visits: format.number(worstStop.visits) }),
          action: { label: t('findings.showStop'), onClick: () => openStop(worstStop.code) },
        }]
      : []),
    ...(report.unexplainedTotal > 0 && topCause
      ? [{
          key: 'unexplained',
          text: t('findings.dwellUnexplained', {
            count: report.unexplainedTotal,
            total: format.number(report.unexplainedTotal),
            cause: t(`dwell.list.causes.${topCause[0]}`),
            share: format.percentWhole(topCause[1] / report.unexplained.length),
          }),
        }]
      : []),
    ...(model.visits > 0
      ? [{ key: 'model', text: t('findings.dwellModel', { perPassenger: `${format.decimal(model.secondsPerPassenger)} s`, base: format.seconds(model.baseSeconds) }) }]
      : []),
  ]
  // Bring the detail into view and give it focus, so keyboard and screen-reader users land on it too.
  useEffect(() => {
    if (!detail) return
    panel.current?.scrollIntoView({ behavior: 'smooth', block: 'start' })
    panel.current?.focus({ preventScroll: true })
  }, [detail])

  return (
    <div className="flex-1 overflow-y-auto p-4 md:px-7 md:pt-6 md:pb-10">
      <header>
        <h1 className="mb-2 text-2xl">{t('dwell.title')}</h1>
        <dl className="flex flex-wrap gap-x-7 gap-y-1.5 [&>div]:flex [&>div]:items-baseline [&>div]:gap-2 [&_dd]:font-semibold [&_dt]:text-ink-2">
          <div>
            <dt>{t('dwell.facts.period')}</dt>
            <dd>
              {report.from ? format.date(report.from) : '?'} – {report.to ? format.date(report.to) : '?'}
            </dd>
          </div>
          <div>
            <dt>{t('dwell.facts.visits')}</dt>
            <dd>{format.number(model.visits)}</dd>
          </div>
          <div>
            <dt>{t('dwell.facts.unexplained')}</dt>
            <dd>{format.number(report.unexplainedTotal)}</dd>
          </div>
        </dl>
        <Findings
          items={findings}
          method={
            <>
              <p className="m-0">
                {model.correlation === null
                  ? t('dwell.modelNoCorrelation', { base: format.seconds(model.baseSeconds), perPassenger: `${format.decimal(model.secondsPerPassenger)} s` })
                  : t('dwell.model', {
                      base: format.seconds(model.baseSeconds),
                      perPassenger: `${format.decimal(model.secondsPerPassenger)} s`,
                      correlation: format.decimal(model.correlation),
                    })}
              </p>
              <p className="m-0">{t('dwell.note')}</p>
            </>
          }
        />
      </header>

      <div className={FILTER_BAR} role="group" aria-label={t('dates.filters')}>
        <SearchSelect
          label={t('dwell.line')}
          value={line === null ? ALL : String(line)}
          options={lineOptions}
          placeholder={t('dwell.searchLine')}
          empty={t('dwell.noOption')}
          match={lineOptionMatch}
          onChange={(value) => {
            onLineChange(value === ALL ? null : Number(value))
            setDetail(null)
          }}
        />
        <DateRangePicker label={t('dates.period')} days={report.days} value={period} onChange={setPeriod} format={format} />
        <DayKindSelect />
      </div>

      <div className="mb-6 grid gap-5 xl:grid-cols-2">
        {layers && <DwellMap layers={layers} stops={report.stops} selected={detail?.kind === 'stop' ? detail.code : null} onSelect={openStop} format={format} />}
        <ChartFigure
          title={t('dwell.bands.title')}
          subtitle={t('dwell.bands.subtitle')}
          chart={<DwellBandsChart bands={report.bands} format={format} height={MAP_SIDE_HEIGHT} />}
          table={<DwellBandsTable bands={report.bands} format={format} />}
        />
      </div>

      {detail && (
        <section ref={panel} tabIndex={-1} aria-labelledby="dwell-detail" className="mb-8 rounded-[10px] border border-route bg-route-soft/40 p-4 outline-none md:p-5">
          <div className="mb-3 flex items-start justify-between gap-4">
            <h2 id="dwell-detail" className="text-xl">
              {detail.kind === 'stop'
                ? t('dwell.stop.heading', { name: stopName(detail.code) })
                : t('dwell.vehicle.heading', { vehicle: detail.vehicle, day: format.date(detail.day) })}
            </h2>
            <button
              className="inline-flex size-8 shrink-0 cursor-pointer items-center justify-center touch-target rounded-lg border border-rule bg-paper text-ink-2 hover:border-ink-2 hover:text-ink"
              onClick={() => setDetail(null)}
              aria-label={t('dwell.close')}
            >
              <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden="true">
                <path d="M6 6l12 12M18 6 6 18" />
              </svg>
            </button>
          </div>
          {detail.kind === 'stop' ? (
            <StopDetail key={`${detail.code}-${line}`} code={detail.code} line={line} period={reportPeriod} format={format} onOpenVehicle={openVehicle} />
          ) : (
            <VehicleDay
              vehicle={detail.vehicle}
              day={detail.day}
              at={detail.at}
              format={format}
              onDayChange={(day) => setDetail({ kind: 'vehicle', vehicle: detail.vehicle, day })}
            />
          )}
        </section>
      )}

      <StopRanking stops={report.stops} minVisits={rules.minVisitsPerStop} selected={detail?.kind === 'stop' ? detail.code : null} onOpen={openStop} format={format} />
      <UnexplainedList report={report} selectedStop={detail?.kind === 'stop' ? detail.code : null} onOpenStop={openStop} onOpenVehicle={openVehicle} format={format} />
    </div>
  )
}
