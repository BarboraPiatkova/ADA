import { createColumnHelper } from '@tanstack/react-table'
import { useMemo } from 'react'
import { useTranslation } from 'react-i18next'
import type { VehicleFault } from '../api'
import type { Format } from '../i18n/format'
import { SortableTable } from '../operations/SortableTable'
import { sortableFeatures } from '../operations/tableFeatures'

const faultCol = createColumnHelper<typeof sortableFeatures, VehicleFault>()
/** Known counting faults (ADA's "Seznam výpadků APC"): unit, from–to, what happened and who found it. */
export function FaultList({ faults, format, subtitle }: { faults: VehicleFault[]; format: Format; subtitle?: string }) {
  const { t, i18n } = useTranslation()
  const columns = useMemo(
    () => [
      faultCol.accessor((f) => f.deviceNumber ?? -1, {
        id: 'device',
        header: t('fleet.detail.faults.device'),
        cell: (info) => info.row.original.deviceNumber ?? t('fleet.detail.faults.wholeVehicle'),
      }),
      faultCol.accessor('from', { id: 'from', header: t('fleet.detail.faults.from'), cell: (info) => <span className="tabular-nums">{format.dateTime(info.getValue())}</span> }),
      faultCol.accessor((f) => f.to ?? '', {
        id: 'to',
        header: t('fleet.detail.faults.to'),
        cell: (info) => <span className="tabular-nums">{info.row.original.to ? format.dateTime(info.row.original.to) : t('fleet.detail.faults.ongoing')}</span>,
      }),
      faultCol.accessor((f) => t(`fleet.detail.faults.kinds.${f.kind}`), { id: 'kind', header: t('fleet.detail.faults.kind') }),
      faultCol.accessor((f) => t(`fleet.detail.faults.sources.${f.source}`), { id: 'source', header: t('fleet.detail.faults.source') }),
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps -- labels change with the language
    [i18n.resolvedLanguage, format],
  )
  return (
    <section aria-labelledby="fleet-faults">
      <h3 id="fleet-faults" className="text-lg">
        {t('fleet.detail.faults.title')}
      </h3>
      <p className="mt-0.5 mb-2.5 text-sm text-ink-2">{subtitle ?? t('fleet.detail.faults.subtitle')}</p>
      <SortableTable
        columns={columns}
        numeric={['device']}
        data={faults}
        rowId={(f) => String(f.id)}
        sorting={[{ id: 'from', desc: false }]}
        empty={t('fleet.detail.faults.none')}
      />
    </section>
  )
}
