import type { TooltipState } from './useChart'

/** Room a tooltip needs before it turns to the other side of the pointer (px). */
const TURN_X = 280
const TURN_Y = 140

/**
 * Values lead, labels follow: the number is the strong element, the label is secondary. Placed in the
 * window, not in the chart box, and turned towards the middle near the window's right or bottom edge.
 */
export function ChartTooltip({ tooltip }: { tooltip: TooltipState | null; width?: number }) {
  if (!tooltip) return null
  const left = tooltip.x > window.innerWidth - TURN_X
  const up = tooltip.y > window.innerHeight - TURN_Y
  return (
    <div
      className="pointer-events-none fixed z-[950] flex min-w-[120px] flex-col gap-px rounded-lg border border-rule bg-paper px-2.5 py-[7px] whitespace-nowrap shadow-float"
      role="status"
      style={{
        left: left ? undefined : tooltip.x + 14,
        right: left ? window.innerWidth - tooltip.x + 14 : undefined,
        top: up ? undefined : tooltip.y + 14,
        bottom: up ? window.innerHeight - tooltip.y + 14 : undefined,
      }}
    >
      {tooltip.content}
    </div>
  )
}
