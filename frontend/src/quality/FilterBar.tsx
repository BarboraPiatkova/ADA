import { useTranslation } from 'react-i18next'
import type { Format } from '../i18n/format'
import { FILTER_BAR, usePeriod } from '../operations/period'
import { DayKindSelect } from '../operations/TimeView'
import { DateRangePicker } from '../ui/DateRangePicker'
import { Select } from '../ui/Select'
import type { Filters } from './filters'
import { tractionLabel } from './labels'
import { METRIC_ORDER, type MetricId } from './metrics'

/**
 * Search, the days (period and kind of day, shared with the statistics screens), traction, and the
 * metric the charts show. It stays at the top while the page scrolls, as on the other screens.
 */
export function FilterBar({
  filters,
  onChange,
  tractions,
  metricId,
  onMetricChange,
  days,
  format,
}: {
  filters: Filters
  onChange: (patch: Partial<Filters>) => void
  tractions: string[]
  metricId: MetricId
  onMetricChange: (id: MetricId) => void
  /** Service days with data, for the period picker. */
  days: string[]
  format: Format
}) {
  const { t } = useTranslation()
  const [period, setPeriod] = usePeriod()
  return (
    <div className={FILTER_BAR} role="search">
      <label className="inline-flex h-8 items-center gap-1.5 rounded-lg touch-target border border-rule bg-paper px-2.5 text-ink-2 focus-within:border-route focus-within:shadow-[0_0_0_1px_var(--route)]">
        <span className="sr-only">{t('health.searchLabel')}</span>
        <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden="true">
          <circle cx="11" cy="11" r="7" />
          <path d="m20 20-3.5-3.5" />
        </svg>
        <input
          className="w-[150px] bg-transparent text-ink outline-none md:w-[220px]"
          type="search"
          value={filters.search}
          placeholder={t('health.searchPlaceholder')}
          onChange={(event) => onChange({ search: event.target.value })}
        />
      </label>
      <DateRangePicker label={t('dates.period')} days={days} value={period} onChange={setPeriod} format={format} />
      <DayKindSelect />
      <Select
        label={t('health.traction')}
        value={filters.traction ?? 'all'}
        options={[{ value: 'all', label: t('health.allTractions') }, ...tractions.map((x) => ({ value: x, label: tractionLabel(t, x) }))]}
        onChange={(value) => onChange({ traction: value === 'all' ? undefined : value })}
      />
      <Select
        label={t('charts.metric')}
        value={metricId}
        options={METRIC_ORDER.map((id) => ({ value: id, label: t(`charts.metrics.${id}`) }))}
        onChange={(value) => onMetricChange(value as MetricId)}
      />
    </div>
  )
}
