import { useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { SegmentedItem, SegmentedRoot } from '../ui/Segmented'

/**
 * Frame for every chart: title, one-line reading guide, optional view controls, and a
 * chart ⇄ table switch. The table view is the accessible twin — every value a chart
 * shows can be read without hovering or seeing colour.
 */
export function ChartFigure({
  title,
  subtitle,
  controls,
  legend,
  chart,
  table,
}: {
  title: string
  subtitle?: string
  controls?: ReactNode
  legend?: ReactNode
  chart: ReactNode
  table: ReactNode
}) {
  const { t } = useTranslation()
  const [view, setView] = useState<'chart' | 'table'>('chart')

  return (
    <figure className="m-0 min-w-0 rounded-[10px] border border-rule bg-paper px-[18px] pt-4 pb-3.5">
      <figcaption className="mb-2.5 flex flex-wrap items-start justify-between gap-x-4 gap-y-2">
        <div>
          <h2 className="text-lg">{title}</h2>
          {subtitle && <p className="mt-0.5 max-w-[72ch] text-sm text-ink-2">{subtitle}</p>}
        </div>
        <div className="flex flex-wrap gap-2">
          {controls}
          <SegmentedRoot
            type="single"
            value={view}
            aria-label={t('charts.viewAs')}
            onValueChange={(value) => value && setView(value as 'chart' | 'table')}
          >
            <SegmentedItem value="chart">
              {t('charts.chart')}
            </SegmentedItem>
            <SegmentedItem value="table">
              {t('charts.table')}
            </SegmentedItem>
          </SegmentedRoot>
        </div>
      </figcaption>
      {view === 'chart' ? (
        <>
          {legend && <div className="mb-2.5">{legend}</div>}
          {chart}
        </>
      ) : (
        <div className="max-h-[360px] overflow-auto rounded-lg border border-rule">{table}</div>
      )}
    </figure>
  )
}
