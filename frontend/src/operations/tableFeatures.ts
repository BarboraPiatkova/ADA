import {
  createPaginatedRowModel,
  createSortedRowModel,
  rowPaginationFeature,
  rowSortingFeature,
  sortFn_alphanumeric,
  sortFn_basic,
  sortFn_datetime,
  sortFn_text,
  tableFeatures,
  type ColumnDef,
  type RowData,
} from '@tanstack/react-table'

/**
 * The sorting functions a column's "auto" sort picks from (text, numbers in text, dates). v9 registers
 * none by default; an unregistered one falls back to a plain comparison and warns.
 */
export const SORT_FNS = { alphanumeric: sortFn_alphanumeric, basic: sortFn_basic, datetime: sortFn_datetime, text: sortFn_text }

// TanStack Table v9 is opt-in per feature: the dwell tables sort and page.
export const sortableFeatures = tableFeatures({
  rowSortingFeature,
  sortedRowModel: createSortedRowModel(),
  sortFns: SORT_FNS,
  rowPaginationFeature,
  paginatedRowModel: createPaginatedRowModel(),
})

export type SortableColumn<T extends RowData> = ColumnDef<typeof sortableFeatures, T, any>
