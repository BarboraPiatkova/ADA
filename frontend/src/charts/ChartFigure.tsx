import { ToggleGroup } from 'radix-ui'
import { useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'

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
          <ToggleGroup.Root
            type="single"
            className="segmented"
            value={view}
            aria-label={t('charts.viewAs')}
            onValueChange={(value) => value && setView(value as 'chart' | 'table')}
          >
            <ToggleGroup.Item value="chart" className="segmented-item">
              {t('charts.chart')}
            </ToggleGroup.Item>
            <ToggleGroup.Item value="table" className="segmented-item">
              {t('charts.table')}
            </ToggleGroup.Item>
          </ToggleGroup.Root>
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
