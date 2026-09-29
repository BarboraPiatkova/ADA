import {
  createPaginatedRowModel,
  createSortedRowModel,
  rowPaginationFeature,
  rowSortingFeature,
  tableFeatures,
  type ColumnDef,
  type RowData,
} from '@tanstack/react-table'

// TanStack Table v9 is opt-in per feature: the dwell tables sort and page.
export const sortableFeatures = tableFeatures({
  rowSortingFeature,
  sortedRowModel: createSortedRowModel(),
  rowPaginationFeature,
  paginatedRowModel: createPaginatedRowModel(),
})

export type SortableColumn<T extends RowData> = ColumnDef<typeof sortableFeatures, T, any>
