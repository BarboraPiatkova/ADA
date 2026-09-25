import type { UseQueryResult } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Empty } from './Empty'
import { LinkButton } from './LinkButton'

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
  if (query.isPending) return <Empty>{loading}</Empty>
  if (query.isError) {
    return (
      <Empty error>
        {t('common.apiUnavailable', { message: query.error.message })}{' '}
        <LinkButton onClick={() => query.refetch()}>{t('common.retry')}</LinkButton>
      </Empty>
    )
  }
  return <>{children(query.data)}</>
}
