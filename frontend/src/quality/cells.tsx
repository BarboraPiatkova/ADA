import type { HealthStatus } from '../api'
import type { Format } from '../i18n/format'
import { cn } from '../ui/cn'
import { Hint } from '../ui/Hint'
import { STATUS_PILL } from '../ui/status'

// Cells of the vehicle table.

/** A share cell coloured by the same thresholds the backend used. */
export function Share({ value, warning, fault, format }: { value: number | undefined; warning: number; fault?: number; format: Format }) {
  if (value === undefined) return <span className="text-ink-2">—</span>
  const level: HealthStatus | null = fault !== undefined && value >= fault ? 'Fault' : value >= warning ? 'Warning' : null
  return (
    <span className={cn('inline-block min-w-[58px] rounded-[5px] px-[7px] py-px', level && [STATUS_PILL[level], 'font-semibold'])}>
      {format.percent(value)}
    </span>
  )
}

export function HeaderHint({ label, hint }: { label: string; hint: string }) {
  return (
    <Hint text={hint}>
      <span className="cursor-help border-b border-dotted border-current">{label}</span>
    </Hint>
  )
}
