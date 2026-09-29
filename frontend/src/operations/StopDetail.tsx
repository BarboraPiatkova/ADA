import { useQuery } from '@tanstack/react-query'
import { createColumnHelper } from '@tanstack/react-table'
import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { StopVisitDwell } from '../api'
import { ChartFigure } from '../charts/ChartFigure'
import type { Format } from '../i18n/format'
import { stopDwellQuery } from '../queries'
import { Empty } from '../ui/Empty'
import { LinkButton } from '../ui/LinkButton'
import { QueryState } from '../ui/QueryState'
import { Select } from '../ui/Select'
import { dayOf, heatCells, median } from './shared'
import { DayTimeline, DwellScatter, HourHeatmap } from './StopCharts'
import { SortableTable } from './SortableTable'
import { sortableFeatures } from './tableFeatures'

const col = createColumnHelper<typeof sortableFeatures, StopVisitDwell>()
const NUMERIC = ['vehicle', 'line', 'dwell', 'passengers', 'expected', 'delay']

/** One stop in depth: when vehicles stand longest, whether passengers explain it, and one day arrival by arrival. */
export function StopDetail({
  code,
  line,
  format,
  onOpenVehicle,
}: {
  code: number
  line: number | null
  format: Format
  onOpenVehicle: (vehicle: number, day: string, at: string) => void
}) {
  const { t } = useTranslation()
  const query = useQuery(stopDwellQuery(code, line))
  return (
    <QueryState query={query} loading={t('dwell.loading')}>
      {(detail) =>
        detail.visits.length === 0 ? (
          <Empty>{t('dwell.noMatch')}</Empty>
        ) : (
          <StopDetailView name={detail.name || String(detail.code)} visits={detail.visits} model={detail.model} format={format} onOpenVehicle={onOpenVehicle} />
        )
      }
    </QueryState>
  )
}

function StopDetailView({
  name,
  visits,
  model,
  format,
  onOpenVehicle,
}: {
  name: string
  visits: StopVisitDwell[]
  model: import('../api').DwellModel
  format: Format
  onOpenVehicle: (vehicle: number, day: string, at: string) => void
}) {
  const { t, i18n } = useTranslation()
  const cells = useMemo(() => heatCells(visits), [visits])
  const days = useMemo(() => [...new Set(visits.map((v) => dayOf(v.arrival)))].sort(), [visits])
  // Start on the day with the most long dwells, the likeliest reason for looking.
  const busiest = useMemo(() => {
    const counts = new Map<string, number>()
    visits.filter((v) => v.unexplained).forEach((v) => counts.set(dayOf(v.arrival), (counts.get(dayOf(v.arrival)) ?? 0) + 1))
    return [...counts.entries()].sort((a, b) => b[1] - a[1])[0]?.[0] ?? days[0]
  }, [visits, days])
  const [day, setDay] = useState(busiest)
  const dayVisits = useMemo(() => visits.filter((v) => dayOf(v.arrival) === day), [visits, day])
  const open = (v: StopVisitDwell) => onOpenVehicle(v.vehicleId, dayOf(v.arrival), v.arrival)
  const signed = (seconds: number) => (seconds > 0 ? '+' : '') + format.seconds(seconds)

  const columns = useMemo(
    () => [
      col.accessor('arrival', { id: 'time', header: t('dwell.list.time'), cell: (info) => <span className="tabular-nums">{format.dateTime(info.getValue())}</span> }),
      col.accessor('vehicleId', {
        id: 'vehicle',
        header: t('dwell.list.vehicle'),
        cell: (info) => (
          <LinkButton className="font-display text-[17px] leading-[1.1] font-bold" onClick={() => open(info.row.original)} aria-label={t('dwell.openVehicle', { vehicle: info.getValue() })}>
            {info.getValue()}
          </LinkButton>
        ),
      }),
      col.accessor((v) => v.line ?? -1, { id: 'line', header: t('dwell.list.line'), cell: (info) => info.row.original.line ?? t('dwell.list.noLine') }),
      col.accessor('dwellSeconds', { id: 'dwell', header: t('dwell.list.dwell'), cell: (info) => <span className={info.row.original.unexplained ? 'font-semibold' : ''}>{format.seconds(info.getValue())}</span> }),
      col.accessor('passengers', { id: 'passengers', header: t('dwell.list.passengers'), cell: (info) => format.number(info.getValue()) }),
      col.accessor('expectedSeconds', { id: 'expected', header: t('dwell.list.expected'), cell: (info) => <span className="text-ink-2">{format.seconds(info.getValue())}</span> }),
      col.accessor('delaySeconds', { id: 'delay', header: t('dwell.list.delay'), cell: (info) => signed(info.getValue()) }),
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps -- labels change with the language
    [i18n.resolvedLanguage, format],
  )
  const table = (rows: StopVisitDwell[]) => (
    <SortableTable columns={columns} numeric={NUMERIC} data={rows} rowId={(v) => `${v.vehicleId}-${v.arrival}`} sorting={[{ id: 'time', desc: false }]} />
  )

  return (
    <div className="grid gap-5">
      <p className="text-sm text-ink-2">
        {t('dwell.stop.summary', {
          visits: format.number(visits.length),
          median: format.seconds(median(visits.map((v) => v.dwellSeconds))),
          unexplained: format.number(visits.filter((v) => v.unexplained).length),
        })}
      </p>
      <div className="grid gap-5 xl:grid-cols-2">
        <ChartFigure
          title={t('dwell.stop.heatmapTitle')}
          subtitle={t('dwell.stop.heatmapSubtitle')}
          chart={<HourHeatmap cells={cells} format={format} />}
          table={table(visits)}
        />
        <ChartFigure
          title={t('dwell.stop.scatterTitle')}
          subtitle={t('dwell.stop.scatterSubtitle')}
          chart={<DwellScatter visits={visits} model={model} format={format} />}
          table={table(visits)}
        />
      </div>
      <ChartFigure
        title={`${t('dwell.stop.timelineTitle')} – ${name}`}
        subtitle={t('dwell.stop.timelineSubtitle')}
        controls={<Select label={t('dwell.day')} value={day} options={days.map((d) => ({ value: d, label: format.date(d) }))} onChange={setDay} />}
        chart={<DayTimeline visits={dayVisits} format={format} onOpen={open} />}
        table={table(dayVisits)}
      />
    </div>
  )
}
