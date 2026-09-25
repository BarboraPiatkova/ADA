import type { UseQueryResult } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'

/** Renders a query's loading and error states the same way everywhere; children get the data. */
export function QueryState<T>({
  query,
  loading,
  children,
}: {
  query: UseQueryResult<T>
  loading: string
  children: (data: T) => ReactNode
}) {
  const { t } = useTranslation()
  if (query.isPending) return <p className="empty">{loading}</p>
  if (query.isError) {
    return (
      <p className="empty error">
        {t('common.apiUnavailable', { message: query.error.message })}{' '}
        <button className="link-button" onClick={() => query.refetch()}>
          {t('common.retry')}
        </button>
      </p>
    )
  }
  return <>{children(query.data)}</>
}
