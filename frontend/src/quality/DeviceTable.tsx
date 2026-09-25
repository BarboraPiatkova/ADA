import { useTable } from '@tanstack/react-table'
import { useMemo } from 'react'
import { useTranslation } from 'react-i18next'
import type { DeviceHealth } from '../api'
import { useFormat } from '../i18n/format'
import { cn } from '../ui/cn'
import { NUM, TABLE, TD, WRAP } from '../ui/table'
import { buildDeviceColumns, DEVICE_NUMERIC, deviceFeatures } from './columns'
import { SortableHeader } from './SortableHeader'

/** A vehicle's counting devices, shown when its row is expanded. */
export function DeviceTable({ devices }: { devices: DeviceHealth[] }) {
  const { t, i18n } = useTranslation()
  const format = useFormat()
  // eslint-disable-next-line react-hooks/exhaustive-deps -- labels change with the language
  const columns = useMemo(() => buildDeviceColumns(t, format), [i18n.resolvedLanguage])
  const table = useTable({
    features: deviceFeatures,
    columns,
    data: devices,
    initialState: { sorting: [{ id: 'deviceNumber', desc: false }] },
  })

  return (
    <table className={cn(TABLE, 'rounded-lg border border-rule bg-paper text-sm')}>
      <thead>
        {table.getHeaderGroups().map((group) => (
          <tr key={group.id}>
            {group.headers.map((header) => (
              <SortableHeader
                key={header.id}
                sorted={header.column.getIsSorted()}
                canSort={header.column.getCanSort()}
                onSort={header.column.getToggleSortingHandler()}
                numeric={DEVICE_NUMERIC.has(header.column.id)}
              >
                <table.FlexRender header={header} />
              </SortableHeader>
            ))}
          </tr>
        ))}
      </thead>
      <tbody>
        {table.getRowModel().rows.map((row) => (
          <tr key={row.id}>
            {row.getAllCells().map((cell) => (
              <td key={cell.id} className={cn(TD, DEVICE_NUMERIC.has(cell.column.id) && NUM, cell.column.id === 'reasons' && WRAP)}>
                <table.FlexRender cell={cell} />
              </td>
            ))}
          </tr>
        ))}
      </tbody>
    </table>
  )
}
