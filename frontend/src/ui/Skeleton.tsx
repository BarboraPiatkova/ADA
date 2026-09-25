import type { ReactNode } from 'react'
import { cn } from './cn'

/**
 * A grey block where content will be. Pulses gently; still under reduced motion (the base
 * layer turns animation off).
 */
export function Skeleton({ className }: { className?: string }) {
  return <span className={cn('block animate-pulse rounded-md bg-surface-2', className)} />
}

/**
 * Wraps a screen's skeleton. The blocks are decorative; assistive technology hears the
 * label instead ("Loading…"), as a polite status.
 */
export function SkeletonScreen({ label, className, children }: { label: string; className?: string; children: ReactNode }) {
  return (
    <div role="status" aria-busy="true" className={className}>
      <span className="sr-only">{label}</span>
      <div aria-hidden="true" className="contents">
        {children}
      </div>
    </div>
  )
}

/** Placeholder rows shaped like a data table: a header and rows of cells of given widths. */
export function TableSkeleton({ columns, rows }: { columns: string[]; rows: number }) {
  return (
    <div className="overflow-hidden rounded-[10px] border border-rule bg-paper">
      <div className="flex gap-6 border-b border-rule bg-surface px-3 py-2.5">
        {columns.map((width, i) => (
          <Skeleton key={i} className={cn('h-3.5', width)} />
        ))}
      </div>
      {Array.from({ length: rows }, (_, row) => (
        <div key={row} className="flex items-center gap-6 border-b border-rule px-3 py-3 last:border-b-0">
          {columns.map((width, i) => (
            <Skeleton key={i} className={cn('h-4', width, i === 0 && 'h-5 rounded-full')} />
          ))}
        </div>
      ))}
    </div>
  )
}
