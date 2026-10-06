import { useTranslation } from 'react-i18next'
import type { DwellBand } from '../api'
import type { Format } from '../i18n/format'
import { cn } from '../ui/cn'
import { NUM, TABLE, TD_COMPACT, TH_COMPACT } from '../ui/table'
import { AXIS_LABEL, AXIS_LABEL_STRONG, BASELINE, CHART_BOX, GRIDLINE, HIT_AREA, MIN_TARGET, TOOLTIP_LABEL, TOOLTIP_VALUE } from '../charts/marks'
import { ChartTooltip } from '../charts/ChartTooltip'
import { FocusRing } from '../charts/FocusRing'
import { useElementWidth, useTooltip } from '../charts/useChart'
import { useRovingFocus } from '../charts/useRovingFocus'

const PLOT_HEIGHT = 200
const LEFT = 48
const RIGHT = 12
const TOP = 16
const AXIS_HEIGHT = 44
const MAX_BAR = 40

/** Round tick step in seconds (5, 10, 15, 30, 60, …) for a 0..max axis. */
function secondsStep(max: number) {
  return [5, 10, 15, 20, 30, 60, 120, 300].find((s) => max / s <= 5) ?? 600
}

function bandLabel(band: DwellBand, t: ReturnType<typeof useTranslation>['t']) {
  if (band.maxPassengers === null) return t('dwell.bands.bandOpen', { from: band.minPassengers })
  if (band.minPassengers === band.maxPassengers) return String(band.minPassengers)
  return t('dwell.bands.band', { from: band.minPassengers, to: band.maxPassengers })
}

/**
 * Dwell by passenger exchange: one column per passenger band, the bar is the median dwell and
 * a tick marks the 90th percentile. One Tab stop; arrow keys move between bands.
 */
export function DwellBandsChart({ bands, format, height = PLOT_HEIGHT }: { bands: DwellBand[]; format: Format; height?: number }) {
  const { t } = useTranslation()
  const [wrap, available] = useElementWidth<HTMLDivElement>()
  const { box, tooltip, show, hide } = useTooltip()
  const roving = useRovingFocus(bands.map((_, i) => [0, i] as const))
  const width = available > 0 ? Math.max(available, LEFT + RIGHT + bands.length * MIN_TARGET * 2) : 0

  const plotWidth = Math.max(0, width - LEFT - RIGHT)
  const band = plotWidth / Math.max(1, bands.length)
  const barWidth = Math.min(MAX_BAR, Math.max(8, band * 0.5))
  const max = Math.max(1, ...bands.map((b) => b.p90Seconds))
  const step = secondsStep(max)
  const yTop = Math.ceil(max / step) * step
  const y = (seconds: number) => TOP + height - (seconds / yTop) * height
  const ticks = Array.from({ length: yTop / step + 1 }, (_, i) => i * step)

  return (
    <div ref={wrap} className="min-w-0">
      <div ref={box} className={CHART_BOX}>
        {width > 0 && (
          <svg width={width} height={TOP + height + AXIS_HEIGHT} role="group" aria-label={t('dwell.bands.keys')}>
            {ticks.map((tick) => (
              <g key={tick}>
                <line x1={LEFT} x2={width - RIGHT} y1={y(tick)} y2={y(tick)} className={tick === 0 ? BASELINE : GRIDLINE} />
                <text x={LEFT - 8} y={y(tick)} className={AXIS_LABEL} textAnchor="end" dominantBaseline="middle">
                  {format.seconds(tick)}
                </text>
              </g>
            ))}
            {bands.map((b, i) => {
              const cx = LEFT + i * band + band / 2
              const x0 = cx - barWidth / 2
              const top = y(b.medianSeconds)
              const r = Math.min(4, y(0) - top)
              const label = bandLabel(b, t)
              const summary = t('dwell.bands.tooltip', { median: format.seconds(b.medianSeconds), p90: format.seconds(b.p90Seconds) })
              const content = (
                <>
                  <strong className={TOOLTIP_VALUE}>{format.seconds(b.medianSeconds)}</strong>
                  <span className={TOOLTIP_LABEL}>
                    {t('dwell.bands.passengers')}: {label} · {summary} · {t('dwell.bands.visits')}: {format.number(b.visits)}
                  </span>
                </>
              )
              const focus = roving.itemProps(0, i, {
                onFocus: (el) => show(el.getBoundingClientRect(), content),
                onBlur: hide,
              })
              return (
                <g key={b.minPassengers}>
                  <rect
                    x={LEFT + i * band}
                    y={TOP}
                    width={band}
                    height={height}
                    className={HIT_AREA}
                    {...focus}
                    role="img"
                    aria-label={`${t('dwell.bands.passengers')} ${label}: ${summary}`}
                    onPointerMove={(event) => show(event, content)}
                    onPointerLeave={hide}
                  />
                  <FocusRing show={roving.isFocused(0, i)} x={LEFT + i * band + 1} y={TOP - 2} width={band - 2} height={height + 3} />
                  {b.visits > 0 && (
                    <>
                      {/* Median: the bar, 4px rounded data end. */}
                      <path
                        className="pointer-events-none fill-series-1"
                        d={`M${x0},${y(0)} V${top + r} Q${x0},${top} ${x0 + r},${top} H${x0 + barWidth - r} Q${x0 + barWidth},${top} ${x0 + barWidth},${top + r} V${y(0)} Z`}
                      />
                      {/* 90th percentile: a whisker up from the bar and a cap. */}
                      <line x1={cx} x2={cx} y1={top} y2={y(b.p90Seconds)} className="pointer-events-none stroke-ink-2" strokeWidth={1.5} />
                      <line x1={cx - 8} x2={cx + 8} y1={y(b.p90Seconds)} y2={y(b.p90Seconds)} className="pointer-events-none stroke-ink-2" strokeWidth={1.5} />
                    </>
                  )}
                  <text x={cx} y={TOP + height + 18} className={AXIS_LABEL_STRONG} textAnchor="middle">
                    {label}
                  </text>
                  <text x={cx} y={TOP + height + 34} className={AXIS_LABEL} textAnchor="middle">
                    {format.number(b.visits)}×
                  </text>
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

export function DwellBandsTable({ bands, format }: { bands: DwellBand[]; format: Format }) {
  const { t } = useTranslation()
  return (
    <table className={TABLE}>
      <thead>
        <tr>
          <th className={TH_COMPACT}>{t('dwell.bands.passengers')}</th>
          <th className={cn(TH_COMPACT, NUM)}>{t('dwell.bands.visits')}</th>
          <th className={cn(TH_COMPACT, NUM)}>{t('dwell.bands.median')}</th>
          <th className={cn(TH_COMPACT, NUM)}>{t('dwell.bands.p90')}</th>
        </tr>
      </thead>
      <tbody>
        {bands.map((b) => (
          <tr key={b.minPassengers}>
            <td className={TD_COMPACT}>{bandLabel(b, t)}</td>
            <td className={cn(TD_COMPACT, NUM)}>{format.number(b.visits)}</td>
            <td className={cn(TD_COMPACT, NUM)}>{format.seconds(b.medianSeconds)}</td>
            <td className={cn(TD_COMPACT, NUM)}>{format.seconds(b.p90Seconds)}</td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}
