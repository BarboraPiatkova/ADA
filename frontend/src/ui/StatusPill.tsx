import { useTranslation } from 'react-i18next'
import type { HealthStatus } from '../api'
import { cn } from './cn'
import { StatusIcon } from './icons'
import { STATUS_PILL } from './status'

/** Status as colour, icon and word together. */
export function StatusPill({ status }: { status: HealthStatus }) {
  const { t } = useTranslation()
  return (
    <span className={cn('inline-flex items-center gap-[5px] rounded-full py-0.5 pr-[9px] pl-1.5 text-sm font-semibold whitespace-nowrap', STATUS_PILL[status])}>
      <StatusIcon status={status} size={14} />
      {t(`health.status.${status}`)}
    </span>
  )
}
