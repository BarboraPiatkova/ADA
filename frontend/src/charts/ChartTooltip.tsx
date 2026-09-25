import type { TooltipState } from './useChart'

/** Values lead, labels follow: the number is the strong element, the label is secondary. */
export function ChartTooltip({ tooltip, width }: { tooltip: TooltipState | null; width: number }) {
  if (!tooltip) return null
  const flip = tooltip.x > width - 220
  return (
    <div
      className="chart-tooltip"
      role="status"
      style={{
        left: flip ? undefined : tooltip.x + 14,
        right: flip ? width - tooltip.x + 14 : undefined,
        top: tooltip.y + 14,
      }}
    >
      {tooltip.content}
    </div>
  )
}
