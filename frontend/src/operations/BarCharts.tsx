import { useTranslation } from 'react-i18next'
import { AXIS_LABEL, BASELINE, CHART_BOX, GRIDLINE, HIT_AREA, LEGEND, SWATCH, TOOLTIP_LABEL, TOOLTIP_VALUE } from '../charts/marks'
import { ChartTooltip } from '../charts/ChartTooltip'
import { FocusRing } from '../charts/FocusRing'
import { useElementWidth, useTooltip } from '../charts/useChart'
import { useRovingFocus } from '../charts/useRovingFocus'
import { cn } from '../ui/cn'

const H = 180
const LEFT = 48
const RIGHT = 12
const TOP = 12
const BOTTOM = 30
const MIN_BAND = 24

/** Round tick step for a 0..max count axis (1, 2, 5, 10, 20, …). */
function niceStep(max: number) {
  const raw = Math.max(1, max / 4)
  const power = 10 ** Math.floor(Math.log10(raw))
  return ([1, 2, 5, 10].find((u) => u * power >= raw) ?? 10) * power
}

/** One stacked segment: a share of its column, drawn in one ramp class. */
export interface Segment {
  label: string
  value: number
  fill: string
  swatch: string
}

/**
 * 100 % stacked columns (e.g. punctuality per hour): each column's segments are shares of its total.
 * One Tab stop; arrow keys move between columns; the tooltip names every segment.
 */
export function StackedShareChart({
  columns,
  keys,
  describe,
}: {
  columns: { key: string | number; label: string; segments: Segment[] }[]
  keys: string
  describe: (column: { label: string; segments: Segment[] }) => string
}) {
  const [wrap, available] = useElementWidth<HTMLDivElement>()
  const { box, tooltip, show, hide } = useTooltip()
  const roving = useRovingFocus(columns.map((_, i) => [0, i] as const))
  const width = available > 0 ? Math.max(available, LEFT + RIGHT + columns.length * MIN_BAND) : 0
  const band = (width - LEFT - RIGHT) / Math.max(1, columns.length)
  const barW = Math.min(28, Math.max(6, band * 0.7))
  const y = (share: number) => TOP + H - share * H
  const legend = columns[0]?.segments ?? []

  return (
    <div ref={wrap} className="min-w-0">
      <div ref={box} className={CHART_BOX}>
        {width > 0 && (
          <svg width={width} height={TOP + H + BOTTOM} role="group" aria-label={keys}>
            {[0, 0.25, 0.5, 0.75, 1].map((tick) => (
              <g key={tick}>
                <line x1={LEFT} x2={width - RIGHT} y1={y(tick)} y2={y(tick)} className={tick === 0 ? BASELINE : GRIDLINE} />
                <text x={LEFT - 8} y={y(tick)} className={AXIS_LABEL} textAnchor="end" dominantBaseline="middle">
                  {Math.round(tick * 100)} %
                </text>
              </g>
            ))}
            {columns.map((column, i) => {
              const total = column.segments.reduce((n, s) => n + s.value, 0)
              const x = LEFT + i * band + (band - barW) / 2
              let acc = 0
              const label = describe(column)
              const content = (
                <>
                  <strong className={TOOLTIP_VALUE}>{column.label}</strong>
                  <span className={TOOLTIP_LABEL}>{label}</span>
                </>
              )
              const focus = roving.itemProps(0, i, { onFocus: (el) => show(el.getBoundingClientRect(), content), onBlur: hide })
              return (
                <g key={column.key}>
                  {total > 0 &&
                    column.segments.map((s) => {
                      const from = acc / total
                      acc += s.value
                      const to = acc / total
                      return s.value > 0 ? <rect key={s.label} x={x} y={y(to)} width={barW} height={y(from) - y(to)} className={s.fill} /> : null
                    })}
                  <rect x={LEFT + i * band} y={TOP} width={band} height={H} className={HIT_AREA} {...focus} role="img" aria-label={label} onPointerMove={(event) => show(event, content)} onPointerLeave={hide} />
                  <FocusRing show={roving.isFocused(0, i)} x={LEFT + i * band + 1} y={TOP - 2} width={band - 2} height={H + 3} />
                  <text x={LEFT + i * band + band / 2} y={TOP + H + 18} className={AXIS_LABEL} textAnchor="middle">
                    {column.label}
                  </text>
                </g>
              )
            })}
          </svg>
        )}
        <ChartTooltip tooltip={tooltip} width={width} />
      </div>
      <ul className={cn(LEGEND, 'm-0 mt-2 list-none p-0')}>
        {legend.map((s) => (
          <li key={s.label}>
            <i className={cn(SWATCH, s.swatch)} aria-hidden="true" /> {s.label}
          </li>
        ))}
      </ul>
    </div>
  )
}

/**
 * Columns with an optional whisker (e.g. median and 90th percentile), one per category. One Tab stop;
 * arrow keys move between columns.
 */
export function ColumnChart({
  columns,
  keys,
  formatValue,
  labelEvery = 1,
}: {
  columns: { key: string | number; label: string; value: number; whisker?: number; description: string }[]
  keys: string
  formatValue: (value: number) => string
  labelEvery?: number
}) {
  const { t } = useTranslation()
  const [wrap, available] = useElementWidth<HTMLDivElement>()
  const { box, tooltip, show, hide } = useTooltip()
  const roving = useRovingFocus(columns.map((_, i) => [0, i] as const))
  const width = available > 0 ? Math.max(available, LEFT + RIGHT + columns.length * MIN_BAND) : 0
  const band = (width - LEFT - RIGHT) / Math.max(1, columns.length)
  const barW = Math.min(28, Math.max(4, band * 0.6))
  const max = Math.max(1, ...columns.map((c) => Math.max(c.value, c.whisker ?? 0)))
  const step = niceStep(max)
  const top = Math.ceil(max / step) * step
  const y = (v: number) => TOP + H - (v / top) * H
  const ticks = Array.from({ length: top / step + 1 }, (_, i) => i * step)
  const every = Math.max(labelEvery, Math.ceil(40 / Math.max(1, band)))

  return (
    <div ref={wrap} className="min-w-0">
      <div ref={box} className={CHART_BOX}>
        {width > 0 && (
          <svg width={width} height={TOP + H + BOTTOM} role="group" aria-label={keys}>
            {ticks.map((tick) => (
              <g key={tick}>
                <line x1={LEFT} x2={width - RIGHT} y1={y(tick)} y2={y(tick)} className={tick === 0 ? BASELINE : GRIDLINE} />
                <text x={LEFT - 8} y={y(tick)} className={AXIS_LABEL} textAnchor="end" dominantBaseline="middle">
                  {formatValue(tick)}
                </text>
              </g>
            ))}
            {columns.map((c, i) => {
              const cx = LEFT + i * band + band / 2
              const x0 = cx - barW / 2
              const content = (
                <>
                  <strong className={TOOLTIP_VALUE}>{formatValue(c.value)}</strong>
                  <span className={TOOLTIP_LABEL}>{c.description}</span>
                </>
              )
              const focus = roving.itemProps(0, i, { onFocus: (el) => show(el.getBoundingClientRect(), content), onBlur: hide })
              const r = Math.min(4, y(0) - y(c.value))
              return (
                <g key={c.key}>
                  {c.value > 0 && (
                    <path
                      className="pointer-events-none fill-series-1"
                      d={`M${x0},${y(0)} V${y(c.value) + r} Q${x0},${y(c.value)} ${x0 + r},${y(c.value)} H${x0 + barW - r} Q${x0 + barW},${y(c.value)} ${x0 + barW},${y(c.value) + r} V${y(0)} Z`}
                    />
                  )}
                  {c.whisker !== undefined && c.whisker > c.value && (
                    <>
                      <line x1={cx} x2={cx} y1={y(c.value)} y2={y(c.whisker)} className="pointer-events-none stroke-ink-2" strokeWidth={1.5} />
                      <line x1={cx - 6} x2={cx + 6} y1={y(c.whisker)} y2={y(c.whisker)} className="pointer-events-none stroke-ink-2" strokeWidth={1.5} />
                    </>
                  )}
                  <rect x={LEFT + i * band} y={TOP} width={band} height={H} className={HIT_AREA} {...focus} role="img" aria-label={c.description} onPointerMove={(event) => show(event, content)} onPointerLeave={hide} />
                  <FocusRing show={roving.isFocused(0, i)} x={LEFT + i * band + 1} y={TOP - 2} width={band - 2} height={H + 3} />
                  {i % every === 0 && (
                    <text x={cx} y={TOP + H + 18} className={AXIS_LABEL} textAnchor="middle">
                      {c.label}
                    </text>
                  )}
                </g>
              )
            })}
          </svg>
        )}
        <ChartTooltip tooltip={tooltip} width={width} />
      </div>
      {columns.length === 0 && <p className="text-sm text-ink-2">{t('dwell.noMatch')}</p>}
    </div>
  )
}
