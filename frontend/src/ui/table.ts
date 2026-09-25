// Shared table styling, so the vehicle table, the device table and the chart table views
// read as one family. Header cells stick to the top of their scroll container.

/** The table itself; the last row drops its bottom rule (the frame provides it). */
export const TABLE = 'w-full border-collapse [&_tbody_tr:last-child>td]:border-b-0'

export const TH =
  'sticky top-0 z-[1] border-b border-rule bg-surface px-3 py-2 text-left align-top font-display text-sm font-semibold whitespace-nowrap text-ink-2'

export const TD = 'border-b border-rule px-3 py-2 text-left align-top whitespace-nowrap'

/** Denser variant for the tables behind charts. */
export const TH_COMPACT = 'sticky top-0 border-b border-rule bg-surface px-2.5 py-[5px] text-left font-display text-sm font-semibold whitespace-nowrap text-ink-2'

export const TD_COMPACT = 'border-b border-rule px-2.5 py-[5px] text-sm whitespace-nowrap'

/** Numbers align right so their digits line up. */
export const NUM = 'text-right'

/** A free-text cell that may wrap. */
export const WRAP = 'min-w-[240px] whitespace-normal'
