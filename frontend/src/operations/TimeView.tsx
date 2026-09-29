import { useTranslation } from 'react-i18next'
import type { DayKind } from '../api'
import { Select } from '../ui/Select'
import { SegmentedItem, SegmentedRoot } from '../ui/Segmented'
import { DAY_KINDS, useDayKind } from './period'

/** How a chart over time is cut: by hour of day, by day of week, or both (a weekday × hour heatmap). */
export type TimeView = 'hour' | 'weekday' | 'week'
const VIEWS: TimeView[] = ['hour', 'weekday', 'week']

export function TimeViewSwitch({ value, onChange }: { value: TimeView; onChange: (view: TimeView) => void }) {
  const { t } = useTranslation()
  return (
    <SegmentedRoot type="single" value={value} aria-label={t('dates.views.label')} onValueChange={(v) => v && onChange(v as TimeView)}>
      {VIEWS.map((v) => (
        <SegmentedItem key={v} value={v}>
          {t(`dates.views.${v}`)}
        </SegmentedItem>
      ))}
    </SegmentedRoot>
  )
}

/** Which days of the week the statistics count; shared by the statistics screens. */
export function DayKindSelect() {
  const { t } = useTranslation()
  const [days, setDays] = useDayKind()
  return (
    <Select
      label={t('dates.days')}
      value={days}
      options={DAY_KINDS.map((d) => ({ value: d, label: t(`dates.kinds.${d}`) }))}
      onChange={(v) => setDays(v as DayKind)}
    />
  )
}
