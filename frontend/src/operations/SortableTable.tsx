import { useTable, type RowData, type SortingState } from '@tanstack/react-table'
import { useRef } from 'react'
import { SortableHeader } from '../quality/SortableHeader'
import { cn } from '../ui/cn'
import { Pagination } from '../ui/Pagination'
import { NUM, TABLE, TD } from '../ui/table'
import { sortableFeatures, type SortableColumn } from './tableFeatures'


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
            {table.getRowModel().rows.map((row) => (
              <tr key={row.id} className={cn('hover:[&>td]:bg-surface', highlight?.(row.original) && '[&>td]:bg-route-soft')}>
                {row.getAllCells().map((cell) => (
                  <td key={cell.id} className={cn(TD, numeric.has(cell.column.id) && NUM)}>
                    <table.FlexRender cell={cell} />
                  </td>
                ))}
              </tr>
            ))}
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
