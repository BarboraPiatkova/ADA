import { useTranslation } from 'react-i18next'
import type { HealthReason } from '../api'

/** Reason codes from the API, phrased in the current language, one per line. */
export function Reasons({ reasons }: { reasons: HealthReason[] }) {
  const { t } = useTranslation()
  if (reasons.length === 0) return <span className="text-ink-2">—</span>
  return (
    <ul className="space-y-0.5">
      {reasons.map((r) => (
        <li key={r.code}>{t(`health.reasons.${r.code}`, { count: r.value ?? 0, value: r.value ?? 0 })}</li>
      ))}
    </ul>
  )
}
