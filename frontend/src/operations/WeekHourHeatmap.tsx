import { useTranslation } from 'react-i18next'
import { AXIS_LABEL, CHART_BOX, HIT_AREA, LEGEND, SWATCH, TOOLTIP_LABEL, TOOLTIP_VALUE } from '../charts/marks'
import { ChartTooltip } from '../charts/ChartTooltip'
import { FocusRing } from '../charts/FocusRing'
import { useElementWidth, useTooltip } from '../charts/useChart'
import { useRovingFocus } from '../charts/useRovingFocus'
import { cn } from '../ui/cn'
import { weekdayNames } from './shared'

const SEQ_FILL = ['fill-seq-1', 'fill-seq-2', 'fill-seq-3', 'fill-seq-4', 'fill-seq-5', 'fill-seq-6']
const SEQ_BG = ['bg-seq-1', 'bg-seq-2', 'bg-seq-3', 'bg-seq-4', 'bg-seq-5', 'bg-seq-6']
const DEFAULT_CELL_H = 26
const LEFT = 44

/** One cell of the week: weekday 1 = Monday … 7 = Sunday, the ramp step (0 = lightest), and its texts. */
export interface WeekHourCell {
  weekday: number
  hour: number
  step: number
  /** Shown big in the tooltip, e.g. "12 %". */
  value: string
  /** The cell in words, for the tooltip and screen readers. */
  label: string
}

/**
 * Weekday × hour on one petrol ramp: rows Monday to Sunday, columns the hours with data. One Tab stop;
 * arrow keys move between cells. `legend` names the ramp steps, lightest first.
 */
export function WeekHourHeatmap({ cells, keys, legend, height }: { cells: WeekHourCell[]; keys: string; legend: string[]; height?: number }) {
  // Seven rows share the height (with the hour axis), no taller than 44 px each.
  const CELL_H = height ? Math.min(44, Math.floor((height - 26) / 7)) : DEFAULT_CELL_H
  const { i18n } = useTranslation()
  const [wrap, available] = useElementWidth<HTMLDivElement>()
  const { box, tooltip, show, hide } = useTooltip()
  const hours = cells.length ? { from: Math.min(...cells.map((c) => c.hour)), to: Math.max(...cells.map((c) => c.hour)) } : { from: 0, to: 23 }
  const hourCount = hours.to - hours.from + 1
  const byKey = new Map(cells.map((c) => [`${c.weekday}-${c.hour}`, c]))
  const roving = useRovingFocus(cells.map((c) => [c.weekday - 1, c.hour - hours.from] as const))
  const cellW = available > 0 ? Math.max(22, (available - LEFT - 8) / hourCount) : 0
  const width = LEFT + cellW * hourCount + 8
  const weekdays = weekdayNames(i18n.resolvedLanguage)

  return (
    <div ref={wrap} className="min-w-0">
      <div ref={box} className={CHART_BOX}>
        {cellW > 0 && (
          <svg width={width} height={CELL_H * 7 + 26} role="group" aria-label={keys}>
            {weekdays.map((name, d) => (
              <text key={name} x={LEFT - 8} y={d * CELL_H + CELL_H / 2} className={AXIS_LABEL} textAnchor="end" dominantBaseline="middle">
                {name}
              </text>
            ))}
            {Array.from({ length: hourCount }, (_, i) => hours.from + i).map((hour, i) => (
              <g key={hour}>
                {Array.from({ length: 7 }, (_, d) => {
                  const cell = byKey.get(`${d + 1}-${hour}`)
                  const x = LEFT + i * cellW
                  const y = d * CELL_H
                  if (!cell) return <rect key={d} x={x + 1} y={y + 1} width={cellW - 2} height={CELL_H - 2} rx={3} className="fill-surface" />
                  const content = (
                    <>
                      <strong className={TOOLTIP_VALUE}>{cell.value}</strong>
                      <span className={TOOLTIP_LABEL}>{cell.label}</span>
                    </>
                  )
                  const focus = roving.itemProps(d, i, { onFocus: (el) => show(el.getBoundingClientRect(), content), onBlur: hide })
                  return (
                    <g key={d}>
                      <rect x={x + 1} y={y + 1} width={cellW - 2} height={CELL_H - 2} rx={3} className={SEQ_FILL[Math.min(cell.step, SEQ_FILL.length - 1)]} />
                      <rect x={x} y={y} width={cellW} height={CELL_H} className={HIT_AREA} {...focus} role="img" aria-label={cell.label} onPointerMove={(event) => show(event, content)} onPointerLeave={hide} />
                      <FocusRing show={roving.isFocused(d, i)} x={x} y={y} width={cellW} height={CELL_H} />
                    </g>
                  )
                })}
                {i % Math.max(1, Math.ceil(28 / cellW)) === 0 && (
                  <text x={LEFT + i * cellW + cellW / 2} y={CELL_H * 7 + 16} className={AXIS_LABEL} textAnchor="middle">
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
        {legend.map((label, i) => (
          <li key={label}>
            <i className={cn(SWATCH, SEQ_BG[i])} aria-hidden="true" />
            {label}
          </li>
        ))}
      </ul>
    </div>
  )
}
