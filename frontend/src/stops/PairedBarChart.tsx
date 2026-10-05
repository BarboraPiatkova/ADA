import { useTranslation } from 'react-i18next'
import { AXIS_LABEL, AXIS_LABEL_STRONG, BASELINE, CHART_BOX, GRIDLINE, HIT_AREA, LEGEND, SWATCH, TOOLTIP_LABEL, TOOLTIP_VALUE } from '../charts/marks'
import { ChartTooltip } from '../charts/ChartTooltip'
import { FocusRing } from '../charts/FocusRing'
import { useElementWidth, useTooltip } from '../charts/useChart'
import { useRovingFocus } from '../charts/useRovingFocus'
import { cn } from '../ui/cn'

const LEFT = 190
const RIGHT = 56
const TOP = 6
const BOTTOM = 26
const BAR = 12
/** Surface gap between a row's two bars. */
const GAP = 2
const ROW = BAR * 2 + GAP + 14

/** Round tick step for a 0..max count axis (1, 2, 5, 10, 20, …). */
function niceStep(max: number) {
  const raw = Math.max(1, max / 4)
  const power = 10 ** Math.floor(Math.log10(raw))
  return ([1, 2, 5, 10].find((u) => u * power >= raw) ?? 10) * power
}

export interface PairedRow {
  key: string | number
  label: string
  /** A second, quieter line under the label (e.g. the direction a stop post serves). */
  sublabel?: string | null
  /** Full name for the tooltip and screen readers; defaults to the label. */
  description?: string
  first: number
  second: number
}

/**
 * Two counts per row as horizontal bars side by side (ADA's stop comparison: boarded and alighted per
 * stop), one count axis. Rows read top to bottom in the order given; values sit at the bar ends. One Tab
 * stop; arrow keys move between rows; the tooltip names both counts.
 */
export function PairedBarChart({
  rows,
  keys,
  names,
  formatValue,
}: {
  rows: PairedRow[]
  /** Keyboard help for screen readers. */
  keys: string
  /** The two series' names, first then second. */
  names: [string, string]
  formatValue: (value: number) => string
}) {
  const { t } = useTranslation()
  const [wrap, width] = useElementWidth<HTMLDivElement>()
  const { box, tooltip, show, hide } = useTooltip()
  const roving = useRovingFocus(rows.map((_, i) => [i, 0] as const))
  const max = Math.max(1, ...rows.map((r) => Math.max(r.first, r.second)))
  const step = niceStep(max)
  const top = Math.ceil(max / step) * step
  const plot = Math.max(1, width - LEFT - RIGHT)
  const x = (v: number) => LEFT + (v / top) * plot
  const ticks = Array.from({ length: top / step + 1 }, (_, i) => i * step)
  const height = TOP + rows.length * ROW + BOTTOM
  // A bar from the axis with its far end rounded (4 px), anchored to the baseline.
  const bar = (y: number, value: number) => {
    const end = x(value)
    const r = Math.min(4, end - LEFT, BAR / 2)
    return `M${LEFT},${y} H${end - r} Q${end},${y} ${end},${y + r} V${y + BAR - r} Q${end},${y + BAR} ${end - r},${y + BAR} H${LEFT} Z`
  }
  const clip = (label: string) => (label.length > 28 ? `${label.slice(0, 27)}…` : label)

  return (
    <div ref={wrap} className="min-w-0">
      <ul className={cn(LEGEND, 'mb-2')}>
        <li>
          <i className={cn(SWATCH, 'bg-series-boardings')} aria-hidden="true" />
          {names[0]}
        </li>
        <li>
          <i className={cn(SWATCH, 'bg-series-alightings')} aria-hidden="true" />
          {names[1]}
        </li>
      </ul>
      <div ref={box} className={CHART_BOX}>
        {width > 0 && rows.length > 0 && (
          <svg width={width} height={height} role="group" aria-label={keys}>
            {ticks.map((tick) => (
              <g key={tick}>
                <line x1={x(tick)} x2={x(tick)} y1={TOP} y2={TOP + rows.length * ROW} className={tick === 0 ? BASELINE : GRIDLINE} />
                <text x={x(tick)} y={TOP + rows.length * ROW + 16} className={AXIS_LABEL} textAnchor="middle">
                  {formatValue(tick)}
                </text>
              </g>
            ))}
            {rows.map((row, i) => {
              const y0 = TOP + i * ROW + 7
              const name = row.description ?? row.label
              const description = `${name}: ${names[0]} ${formatValue(row.first)}, ${names[1]} ${formatValue(row.second)}`
              const content = (
                <>
                  <span className={TOOLTIP_LABEL}>{name}</span>
                  <strong className={TOOLTIP_VALUE}>{formatValue(row.first)}</strong>
                  <span className={TOOLTIP_LABEL}>{names[0]}</span>
                  <strong className={TOOLTIP_VALUE}>{formatValue(row.second)}</strong>
                  <span className={TOOLTIP_LABEL}>{names[1]}</span>
                </>
              )
              const focus = roving.itemProps(i, 0, { onFocus: (el) => show(el.getBoundingClientRect(), content), onBlur: hide })
              return (
                <g key={row.key}>
                  <text x={LEFT - 10} y={row.sublabel ? y0 + BAR / 2 : y0 + BAR + GAP / 2} className={AXIS_LABEL_STRONG} textAnchor="end" dominantBaseline="middle">
                    <title>{name}</title>
                    {clip(row.label)}
                  </text>
                  {row.sublabel && (
                    <text x={LEFT - 10} y={y0 + BAR + GAP + BAR / 2} className={AXIS_LABEL} textAnchor="end" dominantBaseline="middle">
                      {clip(row.sublabel)}
                    </text>
                  )}
                  {row.first > 0 && <path className="pointer-events-none fill-series-boardings" d={bar(y0, row.first)} />}
                  {row.second > 0 && <path className="pointer-events-none fill-series-alightings" d={bar(y0 + BAR + GAP, row.second)} />}
                  <text x={x(row.first) + 6} y={y0 + BAR / 2} className={AXIS_LABEL} dominantBaseline="middle">
                    {formatValue(row.first)}
                  </text>
                  <text x={x(row.second) + 6} y={y0 + BAR + GAP + BAR / 2} className={AXIS_LABEL} dominantBaseline="middle">
                    {formatValue(row.second)}
                  </text>
                  <rect x={0} y={TOP + i * ROW} width={width} height={ROW} className={HIT_AREA} {...focus} role="img" aria-label={description} onPointerMove={(event) => show(event, content)} onPointerLeave={hide} />
                  <FocusRing show={roving.isFocused(i, 0)} x={1} y={TOP + i * ROW + 1} width={width - 2} height={ROW - 2} />
                </g>
              )
            })}
          </svg>
        )}
        <ChartTooltip tooltip={tooltip} width={width} />
      </div>
      {rows.length === 0 && <p className="text-sm text-ink-2">{t('dwell.noMatch')}</p>}
    </div>
  )
}
