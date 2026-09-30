import type { RowFlag } from './RowFlag'

/** The left edge of a flagged row, in its status colour: the same on every table. */
export const FLAG_EDGE: Record<RowFlag['status'], string> = {
  Warning: 'shadow-[inset_4px_0_0_var(--warning)]',
  Fault: 'shadow-[inset_4px_0_0_var(--fault)]',
}
