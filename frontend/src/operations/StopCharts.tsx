import { useTranslation } from 'react-i18next'
import type { DwellModel, StopVisitDwell } from '../api'
import type { Format } from '../i18n/format'
import { cn } from '../ui/cn'
import { AXIS_LABEL, BASELINE, CHART_BOX, GRIDLINE, HIT_AREA, LEGEND, SWATCH, TOOLTIP_LABEL, TOOLTIP_VALUE } from '../charts/marks'
import { ChartTooltip } from '../charts/ChartTooltip'
import { FocusRing } from '../charts/FocusRing'
import { useElementWidth, useTooltip } from '../charts/useChart'
import { useRovingFocus } from '../charts/useRovingFocus'
import type { HeatCell } from './shared'

/** Round tick step in seconds for a 0..max axis. */
function secondsStep(max: number) {
  return [5, 10, 15, 20, 30, 60, 120, 300, 600].find((s) => max / s <= 5) ?? 900
}

// Median-dwell classes for the heatmap: upper bounds in seconds, one petrol step each.
const DWELL_BINS = [15, 25, 40, 60, 120]
const SEQ_FILL = ['fill-seq-1', 'fill-seq-2', 'fill-seq-3', 'fill-seq-4', 'fill-seq-5', 'fill-seq-6']
const SEQ_BG = ['bg-seq-1', 'bg-seq-2', 'bg-seq-3', 'bg-seq-4', 'bg-seq-5', 'bg-seq-6']
const dwellStep = (seconds: number) => {
  const i = DWELL_BINS.findIndex((upper) => seconds <= upper)
  return i === -1 ? DWELL_BINS.length : i
}

const CELL_H = 26
const HEAT_LEFT = 44

/** Hour × weekday, median dwell per cell on one petrol ramp. One Tab stop; arrow keys move between cells. */
export function HourHeatmap({ cells, format }: { cells: HeatCell[]; format: Format }) {
  const { t, i18n } = useTranslation()
  const [wrap, available] = useElementWidth<HTMLDivElement>()
  const { box, tooltip, show, hide } = useTooltip()
  const hours = cells.length ? { from: Math.min(...cells.map((c) => c.hour)), to: Math.max(...cells.map((c) => c.hour)) } : { from: 0, to: 23 }
  const hourCount = hours.to - hours.from + 1
  const byKey = new Map(cells.map((c) => [`${c.weekday}-${c.hour}`, c]))
  const roving = useRovingFocus(cells.map((c) => [c.weekday, c.hour - hours.from] as const))
  const cellW = available > 0 ? Math.max(22, (available - HEAT_LEFT - 8) / hourCount) : 0
  const width = HEAT_LEFT + cellW * hourCount + 8
  const weekdays = Array.from({ length: 7 }, (_, i) => new Intl.DateTimeFormat(i18n.resolvedLanguage, { weekday: 'short' }).format(new Date(2024, 0, 1 + i)))

  return (
    <div ref={wrap} className="min-w-0">
      <div ref={box} className={CHART_BOX}>
        {cellW > 0 && (
          <svg width={width} height={CELL_H * 7 + 26} role="group" aria-label={t('dwell.stop.heatmapKeys')}>
            {weekdays.map((name, d) => (
              <text key={name} x={HEAT_LEFT - 8} y={d * CELL_H + CELL_H / 2} className={AXIS_LABEL} textAnchor="end" dominantBaseline="middle">
                {name}
              </text>
            ))}
            {Array.from({ length: hourCount }, (_, i) => hours.from + i).map((hour, i) => (
              <g key={hour}>
                {Array.from({ length: 7 }, (_, d) => {
                  const cell = byKey.get(`${d}-${hour}`)
                  const x = HEAT_LEFT + i * cellW
                  const y = d * CELL_H
                  if (!cell) return <rect key={d} x={x + 1} y={y + 1} width={cellW - 2} height={CELL_H - 2} rx={3} className="fill-surface" />
                  const label = t('dwell.stop.cell', { day: weekdays[d], hour, median: format.seconds(cell.median), count: cell.count })
                  const content = (
                    <>
                      <strong className={TOOLTIP_VALUE}>{format.seconds(cell.median)}</strong>
                      <span className={TOOLTIP_LABEL}>{label}</span>
                    </>
                  )
                  const focus = roving.itemProps(d, i, { onFocus: (el) => show(el.getBoundingClientRect(), content), onBlur: hide })
                  return (
                    <g key={d}>
                      <rect x={x + 1} y={y + 1} width={cellW - 2} height={CELL_H - 2} rx={3} className={SEQ_FILL[dwellStep(cell.median)]} />
                      <rect
                        x={x}
                        y={y}
                        width={cellW}
                        height={CELL_H}
                        className={HIT_AREA}
                        {...focus}
                        role="img"
                        aria-label={label}
                        onPointerMove={(event) => show(event, content)}
                        onPointerLeave={hide}
                      />
                      <FocusRing show={roving.isFocused(d, i)} x={x} y={y} width={cellW} height={CELL_H} />
                    </g>
                  )
                })}
                {i % Math.max(1, Math.ceil(28 / cellW)) === 0 && (
                  <text x={HEAT_LEFT + i * cellW + cellW / 2} y={CELL_H * 7 + 16} className={AXIS_LABEL} textAnchor="middle">
                    {hour}
                  </text>
                )}
              </g>
            ))}
          </svg>
        )}
        <ChartTooltip tooltip={tooltip} width={width} />
      </div>
      <ul className={cn(LEGEND, 'm-0 mt-2 list-none p-0')}>
        {[0, ...DWELL_BINS].map((from, i) => (
          <li key={from}>
            <i className={cn(SWATCH, SEQ_BG[i])} aria-hidden="true" />
            {i < DWELL_BINS.length ? `${format.seconds(from)} – ${format.seconds(DWELL_BINS[i])}` : `> ${format.seconds(from)}`}
          </li>
        ))}
      </ul>
    </div>
  )
}

const PLOT_H = 220
const LEFT = 52
const RIGHT = 12
const TOP = 12
const BOTTOM = 32

/**
 * Every visit as a dot: passengers across, dwell up. The line is what passengers explain; filled dots
 * are the long dwells they don't. Too many dots to visit one by one, so the table view is the accessible twin.
 */
export function DwellScatter({ visits, model, format }: { visits: StopVisitDwell[]; model: DwellModel; format: Format }) {
  const { t } = useTranslation()
  const [wrap, available] = useElementWidth<HTMLDivElement>()
  const width = available
  const maxX = Math.max(5, ...visits.map((v) => v.passengers))
  const maxYRaw = Math.max(30, ...visits.map((v) => v.dwellSeconds))
  const step = secondsStep(maxYRaw)
  const maxY = Math.ceil(maxYRaw / step) * step
  const plotW = Math.max(0, width - LEFT - RIGHT)
  const x = (p: number) => LEFT + (p / maxX) * plotW
  const y = (s: number) => TOP + PLOT_H - (Math.min(s, maxY) / maxY) * PLOT_H
  const ticks = Array.from({ length: maxY / step + 1 }, (_, i) => i * step)
  const xStep = [1, 2, 5, 10, 20, 50].find((s) => maxX / s <= 8) ?? 100
  const xTicks = Array.from({ length: Math.floor(maxX / xStep) + 1 }, (_, i) => i * xStep)

  return (
    <div ref={wrap} className="min-w-0">
      {width > 0 && (
        <svg width={width} height={TOP + PLOT_H + BOTTOM} role="img" aria-label={t('dwell.stop.scatterKeys')} className="block font-sans">
          {ticks.map((tick) => (
            <g key={tick}>
              <line x1={LEFT} x2={width - RIGHT} y1={y(tick)} y2={y(tick)} className={tick === 0 ? BASELINE : GRIDLINE} />
              <text x={LEFT - 8} y={y(tick)} className={AXIS_LABEL} textAnchor="end" dominantBaseline="middle">
                {format.seconds(tick)}
              </text>
            </g>
          ))}
          {xTicks.map((tick) => (
            <text key={tick} x={x(tick)} y={TOP + PLOT_H + 18} className={AXIS_LABEL} textAnchor="middle">
              {tick}
            </text>
          ))}
          {visits.map((v, i) => (
            <circle
              key={i}
              cx={x(v.passengers)}
              cy={y(v.dwellSeconds)}
              r={v.unexplained ? 3.5 : 2.5}
              className={v.unexplained ? 'fill-ink stroke-paper' : 'fill-series-1 opacity-35'}
            />
          ))}
          <line x1={x(0)} x2={x(maxX)} y1={y(model.baseSeconds)} y2={y(model.baseSeconds + model.secondsPerPassenger * maxX)} className="stroke-route" strokeWidth={2} />
        </svg>
      )}
      <ul className={cn(LEGEND, 'm-0 mt-2 list-none p-0')}>
        <li>
          <i className="inline-block h-0.5 w-4 bg-route" aria-hidden="true" /> {t('dwell.stop.explained')}
        </li>
        <li>
          <i className="inline-block size-2.5 rounded-full bg-series-1 opacity-50" aria-hidden="true" /> {t('dwell.bands.visits')}
        </li>
        <li>
          <i className="inline-block size-2.5 rounded-full bg-ink" aria-hidden="true" /> {t('dwell.stop.unexplainedLegend')}
        </li>
      </ul>
    </div>
  )
}

/** One day at the stop: each arrival a tick at its time, as tall as its dwell. Arrow keys move, Enter opens the vehicle's day. */
export function DayTimeline({ visits, format, onOpen }: { visits: StopVisitDwell[]; format: Format; onOpen: (visit: StopVisitDwell) => void }) {
  const { t } = useTranslation()
  const [wrap, available] = useElementWidth<HTMLDivElement>()
  const { box, tooltip, show, hide } = useTooltip()
  const roving = useRovingFocus(visits.map((_, i) => [0, i] as const))
  const width = available
  const plotW = Math.max(0, width - LEFT - RIGHT)
  const minutes = (iso: string) => { const d = new Date(iso); return d.getHours() * 60 + d.getMinutes() + d.getSeconds() / 60 }
  const x = (iso: string) => LEFT + (minutes(iso) / (24 * 60)) * plotW
  const maxRaw = Math.max(30, ...visits.map((v) => v.dwellSeconds))
  const step = secondsStep(maxRaw)
  const maxY = Math.ceil(maxRaw / step) * step
  const H = 160
  const y = (s: number) => TOP + H - (s / maxY) * H
  const ticks = Array.from({ length: maxY / step + 1 }, (_, i) => i * step)

  return (
    <div ref={wrap} className="min-w-0">
      <div ref={box} className={CHART_BOX}>
        {width > 0 && (
          <svg width={width} height={TOP + H + BOTTOM} role="group" aria-label={t('dwell.stop.timelineKeys')}>
            {ticks.map((tick) => (
              <g key={tick}>
                <line x1={LEFT} x2={width - RIGHT} y1={y(tick)} y2={y(tick)} className={tick === 0 ? BASELINE : GRIDLINE} />
                <text x={LEFT - 8} y={y(tick)} className={AXIS_LABEL} textAnchor="end" dominantBaseline="middle">
                  {format.seconds(tick)}
                </text>
              </g>
            ))}
            {[0, 3, 6, 9, 12, 15, 18, 21, 24].map((h) => (
              <text key={h} x={LEFT + (h / 24) * plotW} y={TOP + H + 18} className={AXIS_LABEL} textAnchor="middle">
                {h}:00
              </text>
            ))}
            {visits.map((v, i) => {
              const cx = x(v.arrival)
              const label = `${format.dateTime(v.arrival)} · ${t('dwell.list.vehicle')} ${v.vehicleId} · ${format.seconds(v.dwellSeconds)} · ${t('dwell.list.passengers')} ${v.passengers}`
              const content = (
                <>
                  <strong className={TOOLTIP_VALUE}>{format.seconds(v.dwellSeconds)}</strong>
                  <span className={TOOLTIP_LABEL}>{label}</span>
                </>
              )
              const focus = roving.itemProps(0, i, { activate: () => onOpen(v), onFocus: (el) => show(el.getBoundingClientRect(), content), onBlur: hide })
              return (
                <g key={i}>
                  <line x1={cx} x2={cx} y1={y(0)} y2={y(v.dwellSeconds)} className={v.unexplained ? 'stroke-ink' : 'stroke-series-1'} strokeWidth={v.unexplained ? 2.5 : 1.5} />
                  <rect
                    x={cx - 5}
                    y={TOP}
                    width={10}
                    height={H}
                    className={cn(HIT_AREA, 'cursor-pointer')}
                    {...focus}
                    role="button"
                    aria-label={label}
                    onClick={() => onOpen(v)}
                    onPointerMove={(event) => show(event, content)}
                    onPointerLeave={hide}
                  />
                  <FocusRing show={roving.isFocused(0, i)} x={cx - 5} y={TOP - 2} width={10} height={H + 3} />
                </g>
              )
            })}
          </svg>
        )}
        <ChartTooltip tooltip={tooltip} width={width} />
      </div>
    </div>
  )
}
