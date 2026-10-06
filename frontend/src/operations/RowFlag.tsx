import { useTranslation } from 'react-i18next'
import { SWATCH } from '../charts/marks'
import { StatusIcon } from '../ui/icons'
import { cn } from '../ui/cn'
import { Hint } from '../ui/Hint'
import { STATUS_TEXT } from '../ui/status'

/** A row over a limit: a warning or a fault, and why, in words. */
export interface RowFlag {
  status: 'Warning' | 'Fault'
  reason: string
}

/** The flag at the start of a row: the status icon, the reason on hover and for screen readers. */
export function FlagMark({ flag }: { flag: RowFlag }) {
  return (
    <Hint text={flag.reason}>
      <span className={cn('mr-1.5 inline-flex align-[-2px]', STATUS_TEXT[flag.status])}>
        <StatusIcon status={flag.status} size={14} />
        <span className="sr-only">{flag.reason}. </span>
      </span>
    </Hint>
  )
}

const STEP_BG = ['', 'bg-map-seq-1', 'bg-map-seq-2', 'bg-map-seq-3', 'bg-map-seq-4', 'bg-map-seq-5']

/**
 * A value's step on the map's petrol ramp, drawn as the map legend draws it: the table reads like the
 * map beside it. The number stays in the page's ink, so it keeps its contrast in both themes.
 */
export function StepSwatch({ step }: { step: number }) {
  return <i className={cn(SWATCH, STEP_BG[step], 'mr-1.5 border border-rule align-[-2px]')} aria-hidden="true" />
}

/** Under a table with flags: what the two marks mean. */
export function FlagLegend({ warning, fault }: { warning: string; fault: string }) {
  const { t } = useTranslation()
  return (
    <p className="mt-2 flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-ink-2">
      <span>{t('flags.legend')}</span>
      <span className="inline-flex items-center gap-1.5">
        <span className={STATUS_TEXT.Warning}>
          <StatusIcon status="Warning" size={14} />
        </span>
        {warning}
      </span>
      <span className="inline-flex items-center gap-1.5">
        <span className={STATUS_TEXT.Fault}>
          <StatusIcon status="Fault" size={14} />
        </span>
        {fault}
      </span>
    </p>
  )
}
