import { useTranslation } from 'react-i18next'
import type { HealthStatus } from '../api'
import { StatusIcon } from '../ui/icons'
import { cn } from '../ui/cn'
import { STATUS_BG, STATUS_FILL, STATUS_ORDER, STATUS_TEXT } from '../ui/status'
import { NUM, TABLE, TD_COMPACT, TH_COMPACT } from '../ui/table'
import { groupTotal, OTHER_KEY, type StatusGroup } from './data'
import { AXIS_LABEL, AXIS_LABEL_STRONG, CHART_BOX, DIMMED, HIT_AREA, LEGEND, SWATCH, TOOLTIP_LABEL, TOOLTIP_VALUE } from './marks'
import { ChartTooltip } from './ChartTooltip'
import { FocusRing } from './FocusRing'
import { useElementWidth, useTooltip } from './useChart'
import { useRovingFocus } from './useRovingFocus'

const ROW = 30
const BAR = 16
const LABEL_WIDTH = 168
const TOTAL_WIDTH = 44
const GAP = 2


/**
 * Part-to-whole per group: how the vehicles of each model (or the devices of each
 * firmware) split by status. Status colours here mean status, and the legend carries
 * icon + word, so colour is never the only cue.
 */
export function StatusBars({
  groups,
  countLabel,
  selectedKey,
  selectedStatus,
  onSelectGroup,
  onSelectSegment,
}: {
  groups: StatusGroup[]
  countLabel: (count: number) => string
  /** The group the reader filtered by; other groups are dimmed. */
  selectedKey?: string
  /** Within the selected group, the status filtered by; other segments are dimmed. */
  selectedStatus?: HealthStatus
  /** Clicking a group's label filters to the group; clicking a segment, to group + status. */
  onSelectGroup?: (key: string) => void
  onSelectSegment?: (key: string, status: HealthStatus) => void
}) {
  const { t } = useTranslation()
  const [wrap, width] = useElementWidth<HTMLDivElement>()
  const { box, tooltip, show, hide } = useTooltip()

  // Group order is set by the caller (largest first, tail folded into "other").
  const maxTotal = Math.max(1, ...groups.map(groupTotal))
  const plotWidth = Math.max(0, width - LABEL_WIDTH - TOTAL_WIDTH)
  const scale = (n: number) => (n / maxTotal) * plotWidth
  // Per row, computed once for both the focus order and the drawing: the statuses present,
  // and whether the row filters (the folded "other" tail doesn't).
  const rows = groups.map((group) => ({
    group,
    present: STATUS_ORDER.filter((s) => group.counts[s] > 0),
    selectable: group.key !== OTHER_KEY,
  }))
  // Focusable marks per row: the type label (when it filters), then one per status present.
  const roving = useRovingFocus(
    rows.flatMap(({ present, selectable }, row) => [
      ...(onSelectGroup && selectable ? [[row, 0] as const] : []),
      ...present.map((_, i) => [row, i + 1] as const),
    ]),
  )

  return (
    <div ref={wrap} className="min-w-0">
      <div ref={box} className={CHART_BOX}>
        {width > 0 && (
          <svg width={width} height={groups.length * ROW} role="group" aria-label={t('charts.statusBarsKeys')}>
            {rows.map(({ group, present, selectable }, row) => {
              const y = row * ROW + (ROW - BAR) / 2
              let x = LABEL_WIDTH
              const canSelect = selectable && !!onSelectSegment
              const groupDimmed = selectedKey !== undefined && selectedKey !== group.key
              const selectGroup = () => selectable && onSelectGroup?.(group.key)
              return (
                <g key={group.key} className={groupDimmed ? DIMMED : undefined}>
                  {onSelectGroup && selectable && (
                    <>
                      <rect
                        x={0}
                        y={row * ROW}
                        width={LABEL_WIDTH - 4}
                        height={ROW}
                        className={cn(HIT_AREA, 'cursor-pointer')}
                        {...roving.itemProps(row, 0, { activate: selectGroup })}
                        role="button"
                        aria-pressed={selectedKey === group.key}
                        aria-label={group.label}
                        onClick={selectGroup}
                      />
                      <FocusRing show={roving.isFocused(row, 0)} x={2} y={row * ROW + 2} width={LABEL_WIDTH - 8} height={ROW - 4} />
                    </>
                  )}
                  <text
                    x={LABEL_WIDTH - 10}
                    y={row * ROW + ROW / 2}
                    className={cn(AXIS_LABEL_STRONG, selectedKey === group.key && 'fill-route font-bold')}
                    textAnchor="end"
                    dominantBaseline="middle"
                    pointerEvents="none"
                  >
                    {group.label.length > 26 ? `${group.label.slice(0, 25)}…` : group.label}
                    <title>{group.label}</title>
                  </text>
                  {present.map((status, i) => {
                    const w = Math.max(2, scale(group.counts[status]) - (i < present.length - 1 ? GAP : 0))
                    const x0 = x
                    x += scale(group.counts[status])
                    const last = i === present.length - 1
                    const segmentDimmed = !groupDimmed && selectedStatus !== undefined && selectedKey === group.key && selectedStatus !== status
                    const selectSegment = () => selectable && onSelectSegment?.(group.key, status)
                    const r = last ? Math.min(4, w) : 0
                    const content = (
                      <>
                        <strong className={TOOLTIP_VALUE}>{countLabel(group.counts[status])}</strong>
                        <span className={TOOLTIP_LABEL}>
                          {t(`health.status.${status}`)}, {group.label}
                        </span>
                      </>
                    )
                    const focus = roving.itemProps(row, i + 1, {
                      activate: canSelect ? selectSegment : undefined,
                      onFocus: (el) => show(el.getBoundingClientRect(), content),
                      onBlur: hide,
                    })
                    return (
                      <g key={status} className="group/segment">
                        <path
                          className={cn(
                            'pointer-events-none group-hover/segment:opacity-80',
                            STATUS_FILL[status],
                            segmentDimmed && DIMMED,
                          )}
                          d={`M${x0},${y} H${x0 + w - r} Q${x0 + w},${y} ${x0 + w},${y + r} V${y + BAR - r} Q${x0 + w},${y + BAR} ${x0 + w - r},${y + BAR} H${x0} Z`}
                        />
                        {/* Hit area the full row tall (30px ≥ 24px). A segment narrower than 24px
                            falls under WCAG 2.5.8's "equivalent" exception: the type label and the
                            status filter pills reach the same filter with full-size targets. */}
                        <rect
                          x={x0}
                          y={row * ROW}
                          width={w}
                          height={ROW}
                          className={cn(HIT_AREA, canSelect && 'cursor-pointer')}
                          {...focus}
                          role={canSelect ? 'button' : 'img'}
                          aria-label={`${group.label}, ${t(`health.status.${status}`)}: ${countLabel(group.counts[status])}`}
                          onPointerMove={(event) => show(event, content)}
                          onPointerLeave={hide}
                          onClick={selectSegment}
                        />
                        <FocusRing show={roving.isFocused(row, i + 1)} x={x0 - 2} y={y - 3} width={w + 4} height={BAR + 6} />
                      </g>
                    )
                  })}
                  <text x={LABEL_WIDTH + scale(groupTotal(group)) + 8} y={row * ROW + ROW / 2} className={AXIS_LABEL} dominantBaseline="middle">
                    {groupTotal(group)}
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
    <ul className={LEGEND}>
      {STATUS_ORDER.map((s) => (
        <li key={s}>
          <span className={cn(SWATCH, STATUS_BG[s])} />
          <span className={cn('-ml-0.5 inline-flex', STATUS_TEXT[s])}>
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
    <table className={TABLE}>
      <thead>
        <tr>
          <th className={TH_COMPACT}>{groupHeader}</th>
          {STATUS_ORDER.map((s) => (
            <th key={s} className={cn(TH_COMPACT, NUM)}>
              {t(`health.status.${s}`)}
            </th>
          ))}
          <th className={cn(TH_COMPACT, NUM)}>{t('charts.total')}</th>
        </tr>
      </thead>
      <tbody>
        {[...groups]
          .sort((a, b) => groupTotal(b) - groupTotal(a))
          .map((g) => (
            <tr key={g.key}>
              <td className={TD_COMPACT}>{g.label}</td>
              {STATUS_ORDER.map((s) => (
                <td key={s} className={cn(TD_COMPACT, NUM)}>
                  {g.counts[s]}
                </td>
              ))}
              <td className={cn(TD_COMPACT, NUM)}>{groupTotal(g)}</td>
            </tr>
          ))}
      </tbody>
    </table>
  )
}
