import type { UseQueryResult } from '@tanstack/react-query'
import type { ReactNode } from 'react'

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
  if (query.isPending) return <p className="empty">{loading}</p>
  if (query.isError) {
    return (
      <p className="empty error">
        API není dostupné: {query.error.message}{' '}
        <button className="link-button" onClick={() => query.refetch()}>
          zkusit znovu
        </button>
      </p>
    )
  }
  return <>{children(query.data)}</>
}
