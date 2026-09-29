import { createColumnHelper } from '@tanstack/react-table'
import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { DwellCause, DwellReport, UnexplainedDwell } from '../api'
import type { Format } from '../i18n/format'
import { cn } from '../ui/cn'
import { LinkButton } from '../ui/LinkButton'
import { SearchInput } from '../ui/SearchInput'
import { Select } from '../ui/Select'
import { dayOf, matches } from './shared'
import { SortableTable } from './SortableTable'
import { sortableFeatures } from './tableFeatures'

const col = createColumnHelper<typeof sortableFeatures, UnexplainedDwell>()
const NUMERIC = ['vehicle', 'line', 'dwell', 'passengers', 'expected', 'delay']
const ALL = 'all'
const CAUSES: DwellCause[] = ['HeldForTimetable', 'SeveralVehicles', 'Other']

// A plain word on a quiet ground, not a status colour: this is a likely reason, not a health verdict.
const CAUSE_CLASS: Record<DwellCause, string> = {
  HeldForTimetable: 'bg-surface-raised text-ink-2',
  SeveralVehicles: 'bg-route-soft text-ink font-semibold',
  Other: 'bg-route-soft text-ink',
}

/** Long dwells the passengers don't explain: searchable, filterable by day and cause, sortable by any column. */
export function UnexplainedList({
  report,
  selectedStop,
  onOpenStop,
  onOpenVehicle,
  format,
}: {
  report: DwellReport
  selectedStop: number | null
  onOpenStop: (code: number) => void
  onOpenVehicle: (vehicle: number, day: string, at: string) => void
  format: Format
}) {
  const { t, i18n } = useTranslation()
  const [search, setSearch] = useState('')
  const [cause, setCause] = useState<DwellCause | typeof ALL>(ALL)
  const [day, setDay] = useState<string>(ALL)
  const days = useMemo(() => [...new Set(report.unexplained.map((r) => dayOf(r.arrival)))].sort(), [report.unexplained])
  const rows = useMemo(
    () =>
      report.unexplained.filter(
        (r) => (cause === ALL || r.cause === cause) && (day === ALL || dayOf(r.arrival) === day) && matches(search, r.stopName, r.stopCode, r.vehicleId, r.line),
      ),
    [report.unexplained, cause, day, search],
  )
  const signed = (seconds: number) => (seconds > 0 ? '+' : '') + format.seconds(seconds)

  const columns = useMemo(
    () => [
      col.accessor('arrival', { id: 'time', header: t('dwell.list.time'), cell: (info) => <span className="tabular-nums">{format.dateTime(info.getValue())}</span> }),
      col.accessor('vehicleId', {
        id: 'vehicle',
        header: t('dwell.list.vehicle'),
        cell: (info) => {
          const r = info.row.original
          return (
            <LinkButton
              className="font-display text-[17px] leading-[1.1] font-bold"
              onClick={() => onOpenVehicle(r.vehicleId, dayOf(r.arrival), r.arrival)}
              aria-label={t('dwell.openVehicle', { vehicle: r.vehicleId })}
            >
              {r.vehicleId}
            </LinkButton>
          )
        },
      }),
      col.accessor((r) => r.line ?? -1, { id: 'line', header: t('dwell.list.line'), cell: (info) => info.row.original.line ?? t('dwell.list.noLine') }),
      col.accessor((r) => r.stopName || String(r.stopCode), {
        id: 'stop',
        header: t('dwell.list.stop'),
        cell: (info) => (
          <LinkButton onClick={() => onOpenStop(info.row.original.stopCode)} aria-label={t('dwell.openStop', { name: info.getValue() })}>
            {info.getValue()}
          </LinkButton>
        ),
      }),
      col.accessor('dwellSeconds', { id: 'dwell', header: t('dwell.list.dwell'), cell: (info) => <span className="font-semibold">{format.seconds(info.getValue())}</span> }),
      col.accessor('passengers', { id: 'passengers', header: t('dwell.list.passengers'), cell: (info) => format.number(info.getValue()) }),
      col.accessor('expectedSeconds', { id: 'expected', header: t('dwell.list.expected'), cell: (info) => <span className="text-ink-2">{format.seconds(info.getValue())}</span> }),
      col.accessor('delaySeconds', { id: 'delay', header: t('dwell.list.delay'), cell: (info) => signed(info.getValue()) }),
      col.accessor('cause', {
        id: 'cause',
        header: t('dwell.list.cause'),
        cell: (info) => {
          const r = info.row.original
          return (
            <span className="inline-flex items-center gap-2">
              <span className={cn('rounded-full px-2 py-0.5 text-sm', CAUSE_CLASS[r.cause])}>{t(`dwell.list.causes.${r.cause}`)}</span>
              {r.otherVehiclesAtOnce > 0 && <span className="text-xs text-ink-2">{t('dwell.list.othersAtOnce', { count: r.otherVehiclesAtOnce })}</span>}
            </span>
          )
        },
      }),
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps -- labels change with the language
    [i18n.resolvedLanguage, onOpenStop, onOpenVehicle, format],
  )

  return (
    <section aria-labelledby="dwell-list">
      <h2 id="dwell-list" className="text-xl">
        {t('dwell.list.title')}
      </h2>
      <p className="mt-0.5 max-w-[72ch] text-sm text-ink-2">
        {t('dwell.list.subtitle', { min: format.seconds(report.rules.minUnexplainedSeconds), excess: format.seconds(report.rules.minExcessSeconds) })}{' '}
        {t('dwell.list.causeHint', { seconds: format.seconds(report.rules.onTimeSeconds), others: report.rules.minOtherVehiclesAtOnce })}
      </p>
      {report.unexplained.length === 0 ? (
        <p className="mt-3 text-ink-2">{t('dwell.list.none')}</p>
      ) : (
        <>
          <div role="search" className="mt-3 mb-2.5 flex flex-wrap items-center gap-3">
            <SearchInput label={t('dwell.search')} placeholder={t('dwell.searchList')} value={search} onChange={setSearch} />
            <Select
              label={t('dwell.day')}
              value={day}
              options={[{ value: ALL, label: t('dwell.allDays') }, ...days.map((d) => ({ value: d, label: format.date(d) }))]}
              onChange={setDay}
            />
            <Select
              label={t('dwell.cause')}
              value={cause}
              options={[{ value: ALL, label: t('dwell.allCauses') }, ...CAUSES.map((c) => ({ value: c, label: t(`dwell.list.causes.${c}`) }))]}
              onChange={(value) => setCause(value as DwellCause | typeof ALL)}
            />
            <span className="text-xs text-ink-2">{t('dwell.list.shown', { shown: format.number(rows.length), total: format.number(report.unexplainedTotal) })}</span>
          </div>
          <SortableTable
            columns={columns}
            numeric={NUMERIC}
            data={rows}
            rowId={(r) => `${r.vehicleId}-${r.arrival}-${r.stopCode}`}
            sorting={[{ id: 'dwell', desc: true }]}
            highlight={(r) => r.stopCode === selectedStop}
            empty={t('dwell.noMatch')}
          />
        </>
      )}
    </section>
  )
}
