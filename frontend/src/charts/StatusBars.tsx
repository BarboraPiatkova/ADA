import { useTranslation } from 'react-i18next'
import type { HealthStatus } from '../api'
import { StatusIcon } from '../ui/icons'
import type { StatusGroup } from './data'
import { ChartTooltip } from './ChartTooltip'
import { useElementWidth, useTooltip } from './useChart'

const STATUSES: HealthStatus[] = ['Fault', 'Warning', 'Ok', 'Unknown']
const ROW = 30
const BAR = 16
const LABEL_WIDTH = 168
const TOTAL_WIDTH = 44
const GAP = 2

const total = (g: StatusGroup) => STATUSES.reduce((n, s) => n + g.counts[s], 0)

/**
 * Part-to-whole per group: how the vehicles of each model (or the devices of each
 * firmware) split by status. Status colours here mean status, and the legend carries
 * icon + word, so colour is never the only cue.
 */
export function StatusBars({ groups, countLabel }: { groups: StatusGroup[]; countLabel: (count: number) => string }) {
  const { t } = useTranslation()
  const [wrap, width] = useElementWidth<HTMLDivElement>()
  const { box, tooltip, show, hide } = useTooltip()

  // Order is set by the caller (largest first, tail folded into "other").
  const sorted = groups
  const maxTotal = Math.max(1, ...sorted.map(total))
  const plotWidth = Math.max(0, width - LABEL_WIDTH - TOTAL_WIDTH)
  const scale = (n: number) => (n / maxTotal) * plotWidth

  return (
    <div ref={wrap} className="chart-canvas">
      <div ref={box} className="chart-box">
        {width > 0 && (
          <svg width={width} height={sorted.length * ROW} className="status-bars">
            {sorted.map((group, row) => {
              const y = row * ROW + (ROW - BAR) / 2
              const present = STATUSES.filter((s) => group.counts[s] > 0)
              let x = LABEL_WIDTH
              return (
                <g key={group.key}>
                  <text x={LABEL_WIDTH - 10} y={row * ROW + ROW / 2} className="axis-label strong" textAnchor="end" dominantBaseline="middle">
                    {group.label.length > 26 ? `${group.label.slice(0, 25)}…` : group.label}
                    <title>{group.label}</title>
                  </text>
                  {present.map((status, i) => {
                    const w = Math.max(2, scale(group.counts[status]) - (i < present.length - 1 ? GAP : 0))
                    const x0 = x
                    x += scale(group.counts[status])
                    const last = i === present.length - 1
                    const r = last ? Math.min(4, w) : 0
                    const content = (
                      <>
                        <strong className="tooltip-value">{countLabel(group.counts[status])}</strong>
                        <span className="tooltip-label">
                          {t(`health.status.${status}`)}, {group.label}
                        </span>
                      </>
                    )
                    return (
                      <path
                        key={status}
                        className={`segment segment-${status.toLowerCase()}`}
                        tabIndex={0}
                        aria-label={`${group.label}, ${t(`health.status.${status}`)}: ${countLabel(group.counts[status])}`}
                        d={`M${x0},${y} H${x0 + w - r} Q${x0 + w},${y} ${x0 + w},${y + r} V${y + BAR - r} Q${x0 + w},${y + BAR} ${x0 + w - r},${y + BAR} H${x0} Z`}
                        onPointerMove={(event) => show(event, content)}
                        onPointerLeave={hide}
                        onFocus={(event) => show(event.currentTarget.getBoundingClientRect(), content)}
                        onBlur={hide}
                      />
                    )
                  })}
                  <text x={LABEL_WIDTH + scale(total(group)) + 8} y={row * ROW + ROW / 2} className="axis-label" dominantBaseline="middle">
                    {total(group)}
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

export function StatusLegend() {
  const { t } = useTranslation()
  return (
    <ul className="status-legend">
      {STATUSES.map((s) => (
        <li key={s}>
          <span className={`legend-swatch segment-${s.toLowerCase()}`} />
          <span className={`legend-icon status-${s.toLowerCase()}`}>
            <StatusIcon status={s} size={14} />
          </span>
          {t(`health.status.${s}`)}
        </li>
      ))}
    </ul>
  )
}

export function StatusBarsTable({ groups, groupHeader }: { groups: StatusGroup[]; groupHeader: string }) {
  const { t } = useTranslation()
  return (
    <table className="data-table compact">
      <thead>
        <tr>
          <th>{groupHeader}</th>
          {STATUSES.map((s) => (
            <th key={s} className="num">
              {t(`health.status.${s}`)}
            </th>
          ))}
          <th className="num">{t('charts.total')}</th>
        </tr>
      </thead>
      <tbody>
        {[...groups]
          .sort((a, b) => total(b) - total(a))
          .map((g) => (
            <tr key={g.key}>
              <td>{g.label}</td>
              {STATUSES.map((s) => (
                <td key={s} className="num">
                  {g.counts[s]}
                </td>
              ))}
              <td className="num">{total(g)}</td>
            </tr>
          ))}
      </tbody>
    </table>
  )
}
