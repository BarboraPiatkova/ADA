import { createColumnHelper } from '@tanstack/react-table'
import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { StopDwell } from '../api'
import type { Format } from '../i18n/format'
import { LinkButton } from '../ui/LinkButton'
import { SearchInput } from '../ui/SearchInput'
import { matches } from './shared'
import { SortableTable } from './SortableTable'
import { sortableFeatures } from './tableFeatures'

const col = createColumnHelper<typeof sortableFeatures, StopDwell>()
const NUMERIC = ['visits', 'median', 'p90', 'passengers', 'excess', 'unexplained']

/** Every stop with enough visits: how long vehicles stand there and how much of it passengers don't explain. */
export function StopRanking({
  stops,
  minVisits,
  selected,
  onOpen,
  format,
}: {
  stops: StopDwell[]
  minVisits: number
  selected: number | null
  onOpen: (code: number) => void
  format: Format
}) {
  const { t, i18n } = useTranslation()
  const [search, setSearch] = useState('')
  const rows = useMemo(() => stops.filter((s) => matches(search, s.name, s.code)), [stops, search])
  const columns = useMemo(
    () => [
      col.accessor((s) => s.name || String(s.code), {
        id: 'stop',
        header: t('dwell.stops.stop'),
        cell: (info) => (
          <LinkButton onClick={() => onOpen(info.row.original.code)} aria-label={t('dwell.openStop', { name: info.getValue() })}>
            {info.getValue()} <span className="text-xs font-normal text-ink-2 tabular-nums">{info.row.original.code}</span>
          </LinkButton>
        ),
      }),
      col.accessor('visits', { id: 'visits', header: t('dwell.stops.visits'), cell: (info) => format.number(info.getValue()) }),
      col.accessor('medianSeconds', { id: 'median', header: t('dwell.stops.median'), cell: (info) => format.seconds(info.getValue()) }),
      col.accessor('p90Seconds', { id: 'p90', header: t('dwell.stops.p90'), cell: (info) => format.seconds(info.getValue()) }),
      col.accessor('meanPassengers', { id: 'passengers', header: t('dwell.stops.meanPassengers'), cell: (info) => format.decimal(info.getValue()) }),
      col.accessor('medianExcessSeconds', {
        id: 'excess',
        header: t('dwell.stops.excess'),
        cell: (info) => (
          <span className="font-semibold">
            {info.getValue() > 0 ? '+' : ''}
            {format.seconds(info.getValue())}
          </span>
        ),
      }),
      col.accessor('unexplained', { id: 'unexplained', header: t('dwell.stops.unexplained'), cell: (info) => format.number(info.getValue()) }),
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps -- labels change with the language
    [i18n.resolvedLanguage, onOpen, format],
  )

  return (
    <section className="mb-8" aria-labelledby="dwell-stops">
      <div className="mb-2.5 flex flex-wrap items-end justify-between gap-x-4 gap-y-2">
        <div>
          <h2 id="dwell-stops" className="text-xl">
            {t('dwell.stops.title')}
          </h2>
          <p className="mt-0.5 max-w-[72ch] text-sm text-ink-2">{t('dwell.stops.subtitle', { min: minVisits })}</p>
        </div>
        <div role="search" className="flex items-center gap-3">
          <SearchInput label={t('dwell.search')} placeholder={t('dwell.searchStops')} value={search} onChange={setSearch} />
          <span className="text-xs text-ink-2">{t('dwell.list.shown', { shown: format.number(rows.length), total: format.number(stops.length) })}</span>
        </div>
      </div>
      <SortableTable
        columns={columns}
        numeric={NUMERIC}
        data={rows}
        rowId={(s) => String(s.code)}
        sorting={[{ id: 'excess', desc: true }]}
        pageSize={25}
        highlight={(s) => s.code === selected}
        empty={t('dwell.noMatch')}
      />
    </section>
  )
}
