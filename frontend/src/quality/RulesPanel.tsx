import { Dialog } from 'radix-ui'
import { Trans, useTranslation } from 'react-i18next'
import type { HealthThresholds } from '../api'
import { useFormat } from '../i18n/format'

/** One rule in the side panel: heading, then short paragraphs. */
const RULE = 'border-t border-rule py-3.5 [&_h3]:mb-1.5 [&_h3]:text-lg [&_p]:mb-2 [&_p]:leading-[1.55] [&_p:last-child]:mb-0'

/**
 * How status is decided, in plain language — opened from the page header, where the
 * question arises, as a side panel the reader can keep open next to the table.
 */
export function RulesPanel({ th }: { th: HealthThresholds }) {
  const { t } = useTranslation()
  const format = useFormat()
  const markup = { strong: <strong />, code: <code />, p: <p /> }
  const rules = [
    { key: 'negative', values: { warning: format.percent(th.negativeOccupancyWarning), fault: format.percent(th.negativeOccupancyFault) } },
    { key: 'silent', values: {} },
    { key: 'imbalance', values: { min: th.minPassengersForBalance, warning: format.percent(th.imbalanceWarning), fault: format.percent(th.imbalanceFault) } },
    { key: 'flagged', values: { warning: format.percent(th.flaggedStopsWarning) } },
  ] as const

  return (
    <Dialog.Root>
      <Dialog.Trigger className="inline-flex cursor-pointer items-center gap-1 align-bottom font-semibold text-route hover:text-route-strong hover:underline">
        <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden="true">
          <circle cx="12" cy="12" r="9" />
          <path d="M12 11v5M12 8h.01" />
        </svg>
        {t('health.rules.open')}
      </Dialog.Trigger>
      <Dialog.Portal>
        <Dialog.Overlay className="fixed inset-0 z-[2000] bg-[rgb(17_26_31/0.28)]" />
        <Dialog.Content className="fixed inset-y-0 right-0 z-[2001] w-[min(460px,100vw)] overflow-y-auto border-l border-rule bg-paper px-6 pt-5 pb-8 shadow-float data-[state=open]:animate-sheet-in">
          <div className="flex items-center justify-between gap-3">
            <Dialog.Title className="text-xl">{t('health.rules.title')}</Dialog.Title>
            <Dialog.Close className="inline-flex cursor-pointer rounded-lg p-1.5 text-ink-2 hover:bg-surface hover:text-ink" aria-label={t('health.rules.close')}>
              <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden="true">
                <path d="M6 6l12 12M18 6 6 18" />
              </svg>
            </Dialog.Close>
          </div>
          <Dialog.Description className="mt-2 mb-[18px] text-ink-2">{t('health.rules.lead')}</Dialog.Description>
          {rules.map((rule) => (
            <section key={rule.key} className={RULE}>
              <h3>{t(`health.rules.${rule.key}.title`)}</h3>
              <Trans i18nKey={`health.rules.${rule.key}.body`} values={rule.values} components={markup} />
            </section>
          ))}
          <section className={RULE}>
            <h3>{t('health.rules.notes.title')}</h3>
            <ul className="list-disc space-y-1.5 pl-[18px] text-ink-2">
              <li>{t('health.rules.notes.counts')}</li>
              <li>{t('health.rules.notes.restarts')}</li>
              <li>{t('health.rules.notes.provisional')}</li>
            </ul>
          </section>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  )
}
