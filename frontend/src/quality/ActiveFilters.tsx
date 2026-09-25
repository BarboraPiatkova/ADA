import type { TFunction } from 'i18next'
import { useTranslation } from 'react-i18next'
import { cn } from '../ui/cn'
import { StatusIcon } from '../ui/icons'
import { LinkButton } from '../ui/LinkButton'
import { STATUS_TEXT } from '../ui/status'
import { hasFilters, NO_FILTERS, type FilterKey, type Filters } from './filters'
import { METRICS } from './metrics'

function Chip({ label, value, onRemove, removeLabel, icon }: { label: string; value: string; onRemove: () => void; removeLabel: string; icon?: React.ReactNode }) {
  return (
    <li className="inline-flex h-[30px] items-center gap-[5px] rounded-full border border-route bg-route-soft pr-1 pl-2.5">
      {icon}
      <span className="text-ink-2">{label}:</span>
      <span className="font-semibold">{value}</span>
      <button className="inline-flex cursor-pointer rounded-full p-1 text-ink-2 hover:bg-paper hover:text-ink" onClick={onRemove} aria-label={removeLabel}>
        <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" aria-hidden="true">
          <path d="M7 7l10 10M17 7 7 17" />
        </svg>
      </button>
    </li>
  )
}

/**
 * The active filters as removable chips — so the reader always sees why the charts and
 * the table show what they show, and can undo any one of them.
 */
export function ActiveFilters({
  filters,
  onChange,
  formatRange,
  tractionLabel,
}: {
  filters: Filters
  onChange: (next: Filters) => void
  formatRange: (metric: keyof typeof METRICS, value: number) => string
  tractionLabel: (t: TFunction, traction: string) => string
}) {
  const { t } = useTranslation()
  if (!hasFilters(filters)) return null

  const remove = (key: FilterKey) => onChange({ ...filters, [key]: key === 'search' ? '' : undefined })
  const removeLabel = (label: string, value: string) => t('filters.remove', { filter: `${label}: ${value}` })

  const chips: { key: FilterKey; label: string; value: string; icon?: React.ReactNode }[] = []
  if (filters.status) {
    chips.push({
      key: 'status',
      label: t('filters.status'),
      value: t(`health.status.${filters.status}`),
      icon: (
        <span className={cn('-ml-0.5 inline-flex', STATUS_TEXT[filters.status])}>
          <StatusIcon status={filters.status} size={14} />
        </span>
      ),
    })
  }
  if (filters.traction) chips.push({ key: 'traction', label: t('filters.traction'), value: tractionLabel(t, filters.traction) })
  if (filters.model !== undefined) chips.push({ key: 'model', label: t('filters.model'), value: filters.model || t('charts.unknownModel') })
  if (filters.firmware !== undefined) chips.push({ key: 'firmware', label: t('filters.firmware'), value: filters.firmware || t('charts.unknownFirmware') })
  if (filters.range) {
    const { metric, from, to } = filters.range
    chips.push({
      key: 'range',
      label: t(`charts.metrics.${metric}`),
      value: to === Infinity ? `≥ ${formatRange(metric, from)}` : `${formatRange(metric, from)} – ${formatRange(metric, to)}`,
    })
  }
  if (filters.search.trim()) chips.push({ key: 'search', label: t('filters.search'), value: `„${filters.search.trim()}“` })

  return (
    <div className="-mt-0.5 mb-4 flex flex-wrap items-center gap-x-3.5 gap-y-2" aria-live="polite">
      <ul className="flex flex-wrap gap-2" aria-label={t('filters.active')}>
        {chips.map((chip) => (
          <Chip
            key={chip.key}
            label={chip.label}
            value={chip.value}
            icon={chip.icon}
            removeLabel={removeLabel(chip.label, chip.value)}
            onRemove={() => remove(chip.key)}
          />
        ))}
      </ul>
      <LinkButton onClick={() => onChange(NO_FILTERS)}>{t('filters.clearAll')}</LinkButton>
    </div>
  )
}
