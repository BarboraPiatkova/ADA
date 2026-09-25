import type { ReactNode } from 'react'
import { cn } from '../ui/cn'
import { NUM, SORT_BUTTON, TH } from '../ui/table'

/**
 * A column header for both tables: sortable columns get a button with the sort direction
 * shown and announced (aria-sort); numeric columns align right.
 */
export function SortableHeader({
  sorted,
  canSort,
  onSort,
  numeric,
  children,
}: {
  sorted: false | 'asc' | 'desc'
  canSort: boolean
  onSort: ((event: unknown) => void) | undefined
  numeric: boolean
  children: ReactNode
}) {
  return (
    <th
      className={cn(TH, numeric && NUM)}
      aria-sort={!canSort ? undefined : sorted === 'asc' ? 'ascending' : sorted === 'desc' ? 'descending' : 'none'}
    >
      {canSort ? (
        <button className={SORT_BUTTON} onClick={onSort}>
          {children}
          <span className="text-route">{sorted === 'asc' ? ' ▴' : sorted === 'desc' ? ' ▾' : ''}</span>
        </button>
      ) : (
        children
      )}
    </th>
  )
}
