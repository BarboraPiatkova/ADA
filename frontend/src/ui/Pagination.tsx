import { useTranslation } from 'react-i18next'
import { useFormat } from '../i18n/format'
import { Select } from './Select'

const PAGE_BUTTON =
  'inline-flex h-8 min-w-8 cursor-pointer items-center justify-center rounded-lg border border-rule bg-paper px-2 font-display font-semibold text-ink tabular-nums hover:enabled:border-ink-2 hover:enabled:bg-surface disabled:cursor-default disabled:text-ink-2 disabled:opacity-45 aria-[current=page]:cursor-default aria-[current=page]:border-route aria-[current=page]:bg-route aria-[current=page]:text-on-route'

/** Page-size choices; the table starts on the first. */
const PAGE_SIZES = [25, 50, 100]

/** Page numbers to show: always first and last, the current page with one neighbour each side, gaps as null. */
function pageList(current: number, count: number): (number | null)[] {
  const pages = new Set([0, count - 1, current - 1, current, current + 1].filter((p) => p >= 0 && p < count))
  const sorted = [...pages].sort((a, b) => a - b)
  const result: (number | null)[] = []
  sorted.forEach((page, i) => {
    if (i > 0 && page - sorted[i - 1] > 1) result.push(null)
    result.push(page)
  })
  return result
}

function Arrow({ direction }: { direction: 'left' | 'right' }) {
  return (
    <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d={direction === 'left' ? 'm15 6-6 6 6 6' : 'm9 6 6 6-6 6'} />
    </svg>
  )
}

/**
 * Pagination bar for a client-side table: the visible range ("26–50 of 104"), page size,
 * and page buttons. Hidden entirely when everything fits on the smallest page.
 */
export function Pagination({
  pageIndex,
  pageSize,
  rowCount,
  onPageChange,
  onPageSizeChange,
}: {
  pageIndex: number
  pageSize: number
  rowCount: number
  onPageChange: (pageIndex: number) => void
  onPageSizeChange: (pageSize: number) => void
}) {
  const { t } = useTranslation()
  const format = useFormat()
  if (rowCount <= PAGE_SIZES[0]) return null

  const pageCount = Math.max(1, Math.ceil(rowCount / pageSize))
  const from = pageIndex * pageSize + 1
  const to = Math.min(rowCount, from + pageSize - 1)

  return (
    <nav className="mt-3 flex flex-wrap items-center gap-x-4 gap-y-2.5" aria-label={t('pagination.label')}>
      <span className="text-ink-2 tabular-nums">
        {t('pagination.range', { from: format.number(from), to: format.number(to), total: format.number(rowCount) })}
      </span>
      <Select
        label={t('pagination.pageSize')}
        value={String(pageSize)}
        options={PAGE_SIZES.map((size) => ({ value: String(size), label: String(size) }))}
        onChange={(value) => onPageSizeChange(Number(value))}
      />
      <div className="flex items-center gap-1 md:ml-auto">
        <button className={PAGE_BUTTON} onClick={() => onPageChange(pageIndex - 1)} disabled={pageIndex === 0} aria-label={t('pagination.previous')}>
          <Arrow direction="left" />
        </button>
        {pageList(pageIndex, pageCount).map((page, i) =>
          page === null ? (
            <span key={`gap-${i}`} className="min-w-5 text-center text-ink-2" aria-hidden="true">
              …
            </span>
          ) : (
            <button
              key={page}
              className={PAGE_BUTTON}
              aria-current={page === pageIndex ? 'page' : undefined}
              aria-label={t('pagination.page', { page: page + 1 })}
              onClick={() => onPageChange(page)}
            >
              {page + 1}
            </button>
          ),
        )}
        <button
          className={PAGE_BUTTON}
          onClick={() => onPageChange(pageIndex + 1)}
          disabled={pageIndex >= pageCount - 1}
          aria-label={t('pagination.next')}
        >
          <Arrow direction="right" />
        </button>
      </div>
    </nav>
  )
}
