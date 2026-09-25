import { ToggleGroup } from 'radix-ui'
import { useTranslation } from 'react-i18next'
import type { HealthStatus } from '../api'
import { useFormat } from '../i18n/format'
import { cn } from '../ui/cn'
import { STATUS_BG, STATUS_ORDER } from '../ui/status'
import { StatusPill } from '../ui/StatusPill'

/** The fleet by status: a proportion bar and one toggle per status that filters by it. */
export function FleetStatus({
  counts,
  selected,
  onSelect,
}: {
  counts: Record<HealthStatus, number>
  selected: HealthStatus | undefined
  onSelect: (status: HealthStatus | undefined) => void
}) {
  const { t } = useTranslation()
  const format = useFormat()
  return (
    <section className="mt-[22px] mb-[18px]" aria-label={t('health.fleetStatus')}>
      {/* Decorative summary of the counts below; the toggle buttons carry the same numbers as text. */}
      <div className="mb-2.5 flex h-3.5 gap-[3px]" aria-hidden="true">
        {STATUS_ORDER.filter((s) => counts[s] > 0).map((s) => (
          <span
            key={s}
            className={cn('min-w-1 rounded-[3px] transition-opacity', STATUS_BG[s], selected && selected !== s && 'opacity-20')}
            style={{ flexGrow: counts[s] }}
          />
        ))}
      </div>
      <ToggleGroup.Root
        type="single"
        className="flex flex-wrap gap-2"
        aria-label={t('health.statusFilter')}
        value={selected ?? ''}
        onValueChange={(value) => onSelect((value || undefined) as HealthStatus | undefined)}
      >
        {STATUS_ORDER.map((s) => (
          <ToggleGroup.Item
            key={s}
            value={s}
            className="inline-flex cursor-pointer items-center gap-2 rounded-full border border-rule bg-paper py-[5px] pr-3 pl-2 touch-target hover:bg-surface data-[state=on]:border-ink data-[state=on]:bg-surface-2"
          >
            <StatusPill status={s} />
            <span className="font-display text-lg leading-none font-bold">{format.number(counts[s])}</span>
          </ToggleGroup.Item>
        ))}
      </ToggleGroup.Root>
    </section>
  )
}
