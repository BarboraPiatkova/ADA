// Chart chrome and mark styles, shared by the SVG charts. Marks stay thin, axes and grid
// stay recessive; the data is the only loud thing.

export const AXIS_LABEL = 'fill-ink-2 text-xs tabular-nums'
export const AXIS_LABEL_STRONG = 'fill-ink text-xs font-medium'
export const GRIDLINE = 'stroke-gridline'
export const BASELINE = 'stroke-axis'

/** Wraps an SVG chart; the tooltip is positioned inside it. */
export const CHART_BOX = 'relative overflow-x-auto [&_svg]:block [&_svg]:font-sans'

/** A selection elsewhere in the same chart dims this mark, as in Power BI. */
export const DIMMED = 'opacity-25'

/** An invisible, focusable hit area larger than the mark it belongs to. */
export const HIT_AREA = 'fill-transparent outline-none'

/**
 * Keyboard focus on a chart mark: a 2px ring in the product colour, drawn as its own
 * shape (CSS outlines on SVG elements render inconsistently across browsers).
 */
export const FOCUS_RING = 'pointer-events-none fill-none stroke-route stroke-2'

/** WCAG 2.2 target size (minimum): 24 CSS px. */
export const MIN_TARGET = 24

/** Legend list: small swatches or icons, each followed by a word. */
export const LEGEND = 'flex flex-wrap gap-x-4 gap-y-1.5 text-xs text-ink-2 [&>li]:inline-flex [&>li]:items-center [&>li]:gap-1.5'
export const SWATCH = 'inline-block size-3.5 rounded-[3px]'

export const TOOLTIP_VALUE = 'font-display text-lg font-bold'
export const TOOLTIP_LABEL = 'text-xs text-ink-2'
