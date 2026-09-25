import { useTranslation } from 'react-i18next'
import type { HistogramBin } from './data'
import { ChartTooltip } from './ChartTooltip'
import { useElementWidth, useTooltip } from './useChart'

const PLOT_HEIGHT = 190
const LEFT = 40
const RIGHT = 12
const TOP = 26 // room for threshold labels
const AXIS_HEIGHT = 28
const MAX_BAR = 24

/** Enter or Space activates a focusable chart mark, like a button. */
const onActivate = (action: () => void) => (event: React.KeyboardEvent) => {
  if (event.key === 'Enter' || event.key === ' ') {
    event.preventDefault()
    action()
  }
}

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
  const [wrap, width] = useElementWidth<HTMLDivElement>()
  const { box, tooltip, show, hide } = useTooltip()

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
    <div ref={wrap} className="chart-canvas">
      <div ref={box} className="chart-box">
        {width > 0 && (
          <svg width={width} height={TOP + PLOT_HEIGHT + AXIS_HEIGHT} className="histogram">
            {ticks.map((tick) => (
              <g key={tick}>
                <line x1={LEFT} x2={width - RIGHT} y1={y(tick)} y2={y(tick)} className={tick === 0 ? 'axis-baseline' : 'gridline'} />
                <text x={LEFT - 8} y={y(tick)} className="axis-label" textAnchor="end" dominantBaseline="middle">
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
                  <strong className="tooltip-value">{countLabel(bin.count)}</strong>
                  <span className="tooltip-label">
                    {formatValue(bin.from)} – {formatValue(bin.to)}
                  </span>
                </>
              )
              return (
                <g key={bin.from}>
                  {/* Hit area: the whole band, taller than the bar, so small bars are easy to hover. */}
                  <rect
                    x={LEFT + i * band}
                    y={TOP}
                    width={band}
                    height={PLOT_HEIGHT}
                    className={`hit-area${onSelect ? ' clickable' : ''}`}
                    tabIndex={0}
                    role={onSelect ? 'button' : undefined}
                    aria-pressed={onSelect ? selectedFrom === bin.from : undefined}
                    aria-label={`${formatValue(bin.from)} – ${formatValue(bin.to)}: ${countLabel(bin.count)}`}
                    onClick={select}
                    onKeyDown={onActivate(select)}
                    onPointerMove={(event) => show(event, content)}
                    onPointerLeave={hide}
                    onFocus={(event) => show(event.currentTarget.getBoundingClientRect(), content)}
                    onBlur={hide}
                  />
                  {bin.count > 0 && (
                    // 4px rounded data end, square at the baseline.
                    <path
                      className={`bar${dimmed ? ' dimmed' : ''}`}
                      d={`M${x0},${y(0)} V${y(bin.count) + r} Q${x0},${y(bin.count)} ${x0 + r},${y(bin.count)} H${x0 + barWidth - r} Q${x0 + barWidth},${y(bin.count)} ${x0 + barWidth},${y(bin.count) + r} V${y(0)} Z`}
                    />
                  )}
                  {i % labelEvery === 0 && (
                    <text x={LEFT + i * band} y={TOP + PLOT_HEIGHT + 18} className="axis-label" textAnchor="middle">
                      {formatValue(bin.from)}
                    </text>
                  )}
                </g>
              )
            })}
            {lines.map((line) => (
              <g key={line.status} className={`threshold threshold-${line.status.toLowerCase()}`}>
                <line x1={x(line.value)} x2={x(line.value)} y1={TOP - 6} y2={TOP + PLOT_HEIGHT} />
                <text x={x(line.value) + 5} y={TOP - 10} className="threshold-label">
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
    <table className="data-table compact">
      <thead>
        <tr>
          <th>{t('charts.range')}</th>
          <th className="num">{countHeader}</th>
        </tr>
      </thead>
      <tbody>
        {bins.map((bin) => (
          <tr key={bin.from}>
            <td>
              {formatValue(bin.from)} – {formatValue(bin.to)}
            </td>
            <td className="num">{bin.count}</td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}
