import type { UseQueryResult } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { ApiError } from '../api'
import { Empty } from './Empty'
import { LinkButton } from './LinkButton'

/**
 * Renders a query's loading and error states the same way everywhere; children get the data.
 * With a skeleton, loading shows the shape of what's coming (the loading text is then its
 * accessible label); without one, the text.
 */
export function QueryState<T>({
  query,
  loading,
  skeleton,
  children,
}: {
  query: UseQueryResult<T>
  loading: string
  skeleton?: ReactNode
  children: (data: T) => ReactNode
}) {
  const { t } = useTranslation()
  if (query.isPending) return skeleton ?? <Empty>{loading}</Empty>
  if (query.isError) {
    // Signed in, but a role in Tokari doesn't include this: retrying won't help.
    if (query.error instanceof ApiError && query.error.status === 403) {
      return <Empty error>{t('common.forbidden')}</Empty>
    }
    return (
      <Empty error>
        {t('common.apiUnavailable', { message: query.error.message })}{' '}
        <LinkButton onClick={() => query.refetch()}>{t('common.retry')}</LinkButton>
      </Empty>
    )
  }
  return <>{children(query.data)}</>
}
