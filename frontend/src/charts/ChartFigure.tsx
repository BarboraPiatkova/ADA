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
    <figure className="chart-figure">
      <figcaption className="chart-head">
        <div>
          <h3 className="chart-title">{title}</h3>
          {subtitle && <p className="chart-subtitle">{subtitle}</p>}
        </div>
        <div className="chart-controls">
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
          {legend && <div className="chart-legend">{legend}</div>}
          {chart}
        </>
      ) : (
        <div className="chart-table-wrap">{table}</div>
      )}
    </figure>
  )
}
