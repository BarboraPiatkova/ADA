import { useId, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { LinkButton } from '../ui/LinkButton'

/** One finding in plain words, optionally with a way to look at it. */
export interface Finding {
  key: string
  text: ReactNode
  action?: { label: string; onClick: () => void }
}

/**
 * What the screen found, before any chart: two or three sentences a planner can act on, each with a
 * link to its detail where there is one. How the numbers are made stays one click away.
 */
export function Findings({ items, method }: { items: Finding[]; method: ReactNode }) {
  const { t } = useTranslation()
  const id = useId()
  return (
    <section aria-labelledby={id} className="mt-4 max-w-[80ch]">
      <h2 id={id} className="text-base font-semibold">
        {t('findings.title')}
      </h2>
      {items.length > 0 ? (
        <ul className="mt-1 mb-0 list-disc space-y-1 pl-5 text-lg marker:text-route">
          {items.map((item) => (
            <li key={item.key}>
              {item.text}
              {item.action && (
                <>
                  {' '}
                  <LinkButton className="text-base" onClick={item.action.onClick}>
                    {item.action.label}
                  </LinkButton>
                </>
              )}
            </li>
          ))}
        </ul>
      ) : (
        <p className="mt-1 text-ink-2">{t('findings.none')}</p>
      )}
      <details className="mt-2 text-sm text-ink-2">
        <summary className="cursor-pointer text-route">{t('findings.method')}</summary>
        <div className="mt-1.5 space-y-1.5">{method}</div>
      </details>
    </section>
  )
}
