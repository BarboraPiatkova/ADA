import type { HealthStatus } from '../api'

// One source for status styling. Status colours mean status only — never a series — and
// always appear with an icon and a word (see StatusPill), never colour alone.

/** Soft background + strong text: pills and tags. */
export const STATUS_PILL: Record<HealthStatus, string> = {
  Fault: 'bg-fault-soft text-fault',
  Warning: 'bg-warning-soft text-warning',
  Ok: 'bg-ok-soft text-ok',
  Unknown: 'bg-unknown-soft text-unknown',
}

/** Icon colour next to a word. */
export const STATUS_TEXT: Record<HealthStatus, string> = {
  Fault: 'text-fault',
  Warning: 'text-warning',
  Ok: 'text-ok',
  Unknown: 'text-unknown',
}

/** Solid fill for HTML marks (the fleet bar, legend swatches). "Too little data" recedes. */
export const STATUS_BG: Record<HealthStatus, string> = {
  Fault: 'bg-fault',
  Warning: 'bg-warning',
  Ok: 'bg-ok',
  Unknown: 'bg-unknown opacity-55',
}

/** Solid fill for SVG marks (chart segments). */
export const STATUS_FILL: Record<HealthStatus, string> = {
  Fault: 'fill-fault',
  Warning: 'fill-warning',
  Ok: 'fill-ok',
  Unknown: 'fill-unknown opacity-55',
}
