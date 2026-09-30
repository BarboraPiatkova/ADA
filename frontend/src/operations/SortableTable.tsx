import { useTable, type RowData, type SortingState } from '@tanstack/react-table'
import { useRef } from 'react'
import { SortableHeader } from '../quality/SortableHeader'
import { cn } from '../ui/cn'
import { Pagination } from '../ui/Pagination'
import { NUM, TABLE, TD } from '../ui/table'
import { FlagMark, type RowFlag } from './RowFlag'
import { sortableFeatures, type SortableColumn } from './tableFeatures'

/** The left edge of a flagged row, in its status colour. */
const FLAG_EDGE: Record<RowFlag['status'], string> = {
  Warning: 'shadow-[inset_4px_0_0_var(--warning)]',
  Fault: 'shadow-[inset_4px_0_0_var(--fault)]',
}

/**
 * A table every dwell view uses: click a header to sort (the direction is shown and announced),
 * paging below. Filtering happens before it, so the caller decides what "matches".
 */
export function SortableTable<T extends RowData>({
  columns,
  numeric: numericIds = [],
  data,
  rowId,
  sorting,
  pageSize = 25,
  highlight,
  flag,
  empty,
}: {
  columns: SortableColumn<T>[]
  /** Ids of the columns holding numbers: right-aligned so digits line up. */
  numeric?: string[]
  data: T[]
  rowId: (row: T) => string
  sorting: SortingState
  pageSize?: number
  /** Rows to mark (e.g. the selected stop). */
  highlight?: (row: T) => boolean
  /** Rows over a limit: a coloured left edge, and an icon with the reason at the start of the row. */
  flag?: (row: T) => RowFlag | null
  empty?: string
}) {
  const top = useRef<HTMLDivElement>(null)
  const numeric = new Set(numericIds)
  const table = useTable({
    features: sortableFeatures,
    columns,
    data,
    getRowId: rowId,
    initialState: { sorting, pagination: { pageIndex: 0, pageSize } },
  })
  const rows = table.getPrePaginatedRowModel().rows
  const { pageIndex, pageSize: size } = table.state.pagination

  return (
    <>
      <div className="overflow-x-auto rounded-[10px] border border-rule" ref={top}>
        <table className={TABLE}>
          <thead>
            {table.getHeaderGroups().map((group) => (
              <tr key={group.id}>
                {group.headers.map((header) => (
                  <SortableHeader
                    key={header.id}
                    sorted={header.column.getIsSorted()}
                    canSort={header.column.getCanSort()}
                    onSort={header.column.getToggleSortingHandler()}
                    numeric={numeric.has(header.column.id)}
                  >
                    <table.FlexRender header={header} />
                  </SortableHeader>
                ))}
              </tr>
            ))}
          </thead>
          <tbody>
            {table.getRowModel().rows.map((row) => {
              const rowFlag = flag?.(row.original) ?? null
              return (
                <tr key={row.id} className={cn('hover:[&>td]:bg-surface', highlight?.(row.original) && '[&>td]:bg-route-soft')}>
                  {row.getAllCells().map((cell, i) => (
                    <td key={cell.id} className={cn(TD, numeric.has(cell.column.id) && NUM, i === 0 && rowFlag && FLAG_EDGE[rowFlag.status])}>
                      {i === 0 && rowFlag && <FlagMark flag={rowFlag} />}
                      <table.FlexRender cell={cell} />
                    </td>
                  ))}
                </tr>
              )
            })}
            {rows.length === 0 && empty && (
              <tr>
                <td colSpan={columns.length} className={cn(TD, 'text-ink-2')}>
                  {empty}
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
      {rows.length > size && (
        <Pagination
          pageIndex={pageIndex}
          pageSize={size}
          rowCount={rows.length}
          onPageChange={(page) => {
            table.setPageIndex(page)
            top.current?.scrollIntoView({ block: 'nearest' })
          }}
          onPageSizeChange={(next) => table.setPageSize(next)}
        />
      )}
    </>
  )
}
