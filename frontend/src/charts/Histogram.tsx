import { useTranslation } from 'react-i18next'
import { cn } from '../ui/cn'
import { NUM, TABLE, TD_COMPACT, TH_COMPACT } from '../ui/table'
import type { HistogramBin } from './data'
import { AXIS_LABEL, BASELINE, CHART_BOX, DIMMED, GRIDLINE, HIT_AREA, MIN_TARGET, TOOLTIP_LABEL, TOOLTIP_VALUE } from './marks'
import { ChartTooltip } from './ChartTooltip'
import { FocusRing } from './FocusRing'
import { useElementWidth, useTooltip } from './useChart'
import { useRovingFocus } from './useRovingFocus'

const PLOT_HEIGHT = 190
const LEFT = 40
const RIGHT = 12
const TOP = 26 // room for threshold labels
const AXIS_HEIGHT = 28
const MAX_BAR = 24

/** Round tick step for a 0..max count axis (1, 2, 5, 10, 20, …). */
function niceStep(max: number) {
  const raw = Math.max(1, max / 4)
  const power = 10 ** Math.floor(Math.log10(raw))
  const unit = [1, 2, 5, 10].find((u) => u * power >= raw) ?? 10
  return unit * power
}

/**
 * Distribution of one measure across vehicles, with the report's warning and fault
 * thresholds drawn where they cut — the view for judging whether a threshold is placed well.
 */
export function Histogram({
  bins,
  thresholds,
  formatValue,
  countLabel,
  selectedFrom,
  onSelect,
}: {
  bins: HistogramBin[]
  thresholds?: { warning: number; fault?: number }
  formatValue: (value: number) => string
  countLabel: (count: number) => string
  /** Lower edge of the bin the reader filtered by; other bins are dimmed. */
  selectedFrom?: number
  /** Clicking a bin filters everything else to it (cross-filtering). */
  onSelect?: (bin: HistogramBin, isLast: boolean) => void
}) {
  const { t } = useTranslation()
  const [wrap, available] = useElementWidth<HTMLDivElement>()
  const { box, tooltip, show, hide } = useTooltip()
  const roving = useRovingFocus(bins.map((_, i) => [0, i] as const))
  // Each column stays a usable target; on a narrow screen the chart scrolls in its card.
  const width = available > 0 ? Math.max(available, LEFT + RIGHT + bins.length * MIN_TARGET) : 0

  const top = bins.length ? bins[bins.length - 1].to : 1
  const plotWidth = Math.max(0, width - LEFT - RIGHT)
  const band = plotWidth / Math.max(1, bins.length)
  const barWidth = Math.min(MAX_BAR, Math.max(3, band - 4))
  const maxCount = Math.max(1, ...bins.map((b) => b.count))
  const tickStep = niceStep(maxCount)
  const yTop = Math.ceil(maxCount / tickStep) * tickStep
  const y = (count: number) => TOP + PLOT_HEIGHT - (count / yTop) * PLOT_HEIGHT
  const x = (value: number) => LEFT + (value / top) * plotWidth
  const ticks = Array.from({ length: yTop / tickStep + 1 }, (_, i) => i * tickStep)
  const labelEvery = Math.max(1, Math.ceil(56 / band))

  const lines = thresholds
    ? [
        { status: 'Warning' as const, value: thresholds.warning, label: t('charts.thresholdWarning') },
        ...(thresholds.fault !== undefined ? [{ status: 'Fault' as const, value: thresholds.fault, label: t('charts.thresholdFault') }] : []),
      ].filter((line) => line.value <= top)
    : []

  return (
    <div ref={wrap} className="min-w-0">
      <div ref={box} className={CHART_BOX}>
        {width > 0 && (
          <svg width={width} height={TOP + PLOT_HEIGHT + AXIS_HEIGHT} role="group" aria-label={t('charts.histogramKeys')}>
            {ticks.map((tick) => (
              <g key={tick}>
                <line x1={LEFT} x2={width - RIGHT} y1={y(tick)} y2={y(tick)} className={tick === 0 ? BASELINE : GRIDLINE} />
                <text x={LEFT - 8} y={y(tick)} className={AXIS_LABEL} textAnchor="end" dominantBaseline="middle">
                  {tick}
                </text>
              </g>
            ))}
            {bins.map((bin, i) => {
              const cx = LEFT + i * band + band / 2
              const h = y(0) - y(bin.count)
              const r = Math.min(4, h)
              const x0 = cx - barWidth / 2
              const dimmed = selectedFrom !== undefined && selectedFrom !== bin.from
              const select = () => onSelect?.(bin, i === bins.length - 1)
              const content = (
                <>
                  <strong className={TOOLTIP_VALUE}>{countLabel(bin.count)}</strong>
                  <span className={TOOLTIP_LABEL}>
                    {formatValue(bin.from)} – {formatValue(bin.to)}
                  </span>
                </>
              )
              const focus = roving.itemProps(0, i, {
                activate: onSelect ? select : undefined,
                onFocus: (el) => show(el.getBoundingClientRect(), content),
                onBlur: hide,
              })
              return (
                <g key={bin.from}>
                  {/* Hit area: the whole band, taller than the bar, so small bars are easy to hover. */}
                  <rect
                    x={LEFT + i * band}
                    y={TOP}
                    width={band}
                    height={PLOT_HEIGHT}
                    className={cn(HIT_AREA, onSelect && 'cursor-pointer')}
                    {...focus}
                    role={onSelect ? 'button' : 'img'}
                    aria-pressed={onSelect ? selectedFrom === bin.from : undefined}
                    aria-label={`${formatValue(bin.from)} – ${formatValue(bin.to)}: ${countLabel(bin.count)}`}
                    onClick={select}
                    onPointerMove={(event) => show(event, content)}
                    onPointerLeave={hide}
                  />
                  <FocusRing show={roving.isFocused(0, i)} x={LEFT + i * band + 1} y={TOP - 2} width={band - 2} height={PLOT_HEIGHT + 3} />
                  {bin.count > 0 && (
                    // 4px rounded data end, square at the baseline.
                    <path
                      className={cn('pointer-events-none fill-series-1', dimmed && DIMMED)}
                      d={`M${x0},${y(0)} V${y(bin.count) + r} Q${x0},${y(bin.count)} ${x0 + r},${y(bin.count)} H${x0 + barWidth - r} Q${x0 + barWidth},${y(bin.count)} ${x0 + barWidth},${y(bin.count) + r} V${y(0)} Z`}
                    />
                  )}
                  {i % labelEvery === 0 && (
                    <text x={LEFT + i * band} y={TOP + PLOT_HEIGHT + 18} className={AXIS_LABEL} textAnchor="middle">
                      {formatValue(bin.from)}
                    </text>
                  )}
                </g>
              )
            })}
            {lines.map((line) => (
              <g key={line.status}>
                <line x1={x(line.value)} x2={x(line.value)} y1={TOP - 6} y2={TOP + PLOT_HEIGHT} className={cn('stroke-2', line.status === 'Fault' ? 'stroke-fault' : 'stroke-warning')} />
                <text x={x(line.value) + 5} y={TOP - 10} className="fill-ink-2 text-xs font-semibold">
                  {line.label} {formatValue(line.value)}
                </text>
              </g>
            ))}
          </svg>
        )}
        <ChartTooltip tooltip={tooltip} width={width} />
      </div>
    </div>
  )
}

export function HistogramTable({ bins, formatValue, countHeader }: { bins: HistogramBin[]; formatValue: (value: number) => string; countHeader: string }) {
  const { t } = useTranslation()
  return (
    <table className={TABLE}>
      <thead>
        <tr>
          <th className={TH_COMPACT}>{t('charts.range')}</th>
          <th className={cn(TH_COMPACT, NUM)}>{countHeader}</th>
        </tr>
      </thead>
      <tbody>
        {bins.map((bin) => (
          <tr key={bin.from}>
            <td className={TD_COMPACT}>
              {formatValue(bin.from)} – {formatValue(bin.to)}
            </td>
            <td className={cn(TD_COMPACT, NUM)}>{bin.count}</td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}
