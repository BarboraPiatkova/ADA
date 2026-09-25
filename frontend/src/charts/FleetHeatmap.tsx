import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { VehicleDay, VehicleHealth } from '../api'
import { useFormat } from '../i18n/format'
import { binOf, type Metric } from '../quality/metrics'
import { ChartTooltip } from './ChartTooltip'
import { useElementWidth, useTooltip } from './useChart'

const DAY_LABEL_WIDTH = 64
const ROW_HEIGHT = 24
const AXIS_HEIGHT = 26
const GAP = 2 // surface gap between cells

/**
 * Fleet × day heatmap: one column per vehicle (in the table's current order), one row per
 * operating day, colour = the chosen metric on a one-hue sequential scale. An empty
 * outlined cell means no data that day — never "zero".
 */
export function FleetHeatmap({
  vehicles,
  days,
  daily,
  metric,
  formatValue,
  onSelectVehicle,
}: {
  vehicles: VehicleHealth[]
  days: string[]
  daily: Map<string, VehicleDay>
  metric: Metric
  formatValue: (value: number) => string
  onSelectVehicle: (vehicleId: number) => void
}) {
  const { t } = useTranslation()
  const format = useFormat()
  const [wrap, width] = useElementWidth<HTMLDivElement>()
  const { box, tooltip, show, hide } = useTooltip()
  const [hovered, setHovered] = useState<{ column: number; row: number } | null>(null)

  const cellWidth = Math.max(5, Math.min(22, Math.floor((width - DAY_LABEL_WIDTH) / Math.max(1, vehicles.length))))
  const plotWidth = cellWidth * vehicles.length
  const height = days.length * ROW_HEIGHT + AXIS_HEIGHT
  // Label every k-th vehicle so labels stay ≥ 40 px apart.
  const labelEvery = Math.max(1, Math.ceil(40 / cellWidth))

  const cells = useMemo(
    () =>
      vehicles.flatMap((vehicle, column) =>
        days.map((day, row) => {
          const record = daily.get(`${vehicle.vehicleId}|${day}`)
          const value = record ? metric.day(record) : null
          return { vehicle, day, column, row, record, value }
        }),
      ),
    [vehicles, days, daily, metric],
  )

  const cellAt = (event: React.MouseEvent<SVGSVGElement>) => {
    const rect = event.currentTarget.getBoundingClientRect()
    const column = Math.floor((event.clientX - rect.left - DAY_LABEL_WIDTH) / cellWidth)
    const row = Math.floor((event.clientY - rect.top) / ROW_HEIGHT)
    if (column < 0 || column >= vehicles.length || row < 0 || row >= days.length) return null
    return cells[column * days.length + row]
  }

  if (vehicles.length === 0 || days.length === 0) {
    return <p className="empty">{t('charts.nothingToShow')}</p>
  }

  return (
    <div ref={wrap} className="chart-canvas">
      <div ref={box} className="chart-box">
        {width > 0 && (
          <svg
            width={DAY_LABEL_WIDTH + plotWidth}
            height={height}
            className="heatmap"
            role="img"
            aria-label={t('charts.heatmapAria', { metric: t(`charts.metrics.${metric.id}`), vehicles: vehicles.length, days: days.length })}
            onPointerMove={(event) => {
              const cell = cellAt(event)
              if (!cell) {
                setHovered(null)
                hide()
                return
              }
              setHovered({ column: cell.column, row: cell.row })
              show(
                event,
                <>
                  <strong className="tooltip-value">{cell.value === null ? t('charts.noData') : formatValue(cell.value)}</strong>
                  <span className="tooltip-label">
                    {t('charts.vehicleDay', { vehicle: cell.vehicle.vehicleId, day: format.dayShort(cell.day) })}
                  </span>
                  {cell.record && <span className="tooltip-label">{t('charts.stopsThatDay', { count: cell.record.stopSummaries })}</span>}
                </>,
              )
            }}
            onPointerLeave={() => {
              setHovered(null)
              hide()
            }}
            onClick={(event) => {
              const cell = cellAt(event)
              if (cell) onSelectVehicle(cell.vehicle.vehicleId)
            }}
          >
            {days.map((day, row) => (
              <text key={day} x={DAY_LABEL_WIDTH - 10} y={row * ROW_HEIGHT + ROW_HEIGHT / 2} className="axis-label" textAnchor="end" dominantBaseline="middle">
                {format.dayShort(day)}
              </text>
            ))}
            {cells.map((cell) => {
              const x = DAY_LABEL_WIDTH + cell.column * cellWidth
              const y = cell.row * ROW_HEIGHT
              return cell.value === null ? (
                <rect
                  key={`${cell.vehicle.vehicleId}|${cell.day}`}
                  x={x + 0.5}
                  y={y + 0.5}
                  width={cellWidth - GAP - 1}
                  height={ROW_HEIGHT - GAP - 1}
                  rx={2}
                  className="heat-empty"
                />
              ) : (
                <rect
                  key={`${cell.vehicle.vehicleId}|${cell.day}`}
                  x={x}
                  y={y}
                  width={cellWidth - GAP}
                  height={ROW_HEIGHT - GAP}
                  rx={2}
                  fill={`var(--seq-${binOf(metric, cell.value) + 1})`}
                />
              )
            })}
            {hovered && (
              <rect
                x={DAY_LABEL_WIDTH + hovered.column * cellWidth - 1}
                y={-1}
                width={cellWidth}
                height={days.length * ROW_HEIGHT}
                rx={3}
                className="heat-column-hover"
              />
            )}
            {vehicles.map((vehicle, column) =>
              column % labelEvery === 0 ? (
                <text
                  key={vehicle.vehicleId}
                  x={DAY_LABEL_WIDTH + column * cellWidth + (cellWidth - GAP) / 2}
                  y={days.length * ROW_HEIGHT + 16}
                  className="axis-label"
                  textAnchor="middle"
                >
                  {vehicle.vehicleId}
                </text>
              ) : null,
            )}
          </svg>
        )}
        <ChartTooltip tooltip={tooltip} width={width} />
      </div>
    </div>
  )
}

/** Scale legend for the heatmap: six bins of the sequential ramp, plus "no data". */
export function HeatmapLegend({ metric, formatValue }: { metric: Metric; formatValue: (value: number) => string }) {
  const { t } = useTranslation()
  const labels = metric.bins.map((edge, i) => {
    const lower = i === 0 ? 0 : metric.bins[i - 1]
    return edge === Infinity ? `≥ ${formatValue(lower)}` : i === 0 ? `< ${formatValue(edge)}` : `${formatValue(lower)} – ${formatValue(edge)}`
  })
  return (
    <ul className="scale-legend">
      {labels.map((label, i) => (
        <li key={label}>
          <span className="scale-swatch" style={{ background: `var(--seq-${i + 1})` }} />
          {label}
        </li>
      ))}
      <li>
        <span className="scale-swatch no-data" />
        {t('charts.noData')}
      </li>
    </ul>
  )
}

/** Table twin of the heatmap. */
export function HeatmapTable({
  vehicles,
  days,
  daily,
  metric,
  formatValue,
}: {
  vehicles: VehicleHealth[]
  days: string[]
  daily: Map<string, VehicleDay>
  metric: Metric
  formatValue: (value: number) => string
}) {
  const { t } = useTranslation()
  const format = useFormat()
  return (
    <table className="data-table compact">
      <thead>
        <tr>
          <th>{t('health.columns.vehicle')}</th>
          {days.map((day) => (
            <th key={day} className="num">
              {format.dayShort(day)}
            </th>
          ))}
        </tr>
      </thead>
      <tbody>
        {vehicles.map((vehicle) => (
          <tr key={vehicle.vehicleId}>
            <td>{vehicle.vehicleId}</td>
            {days.map((day) => {
              const record = daily.get(`${vehicle.vehicleId}|${day}`)
              const value = record ? metric.day(record) : null
              return (
                <td key={day} className="num">
                  {value === null ? '—' : formatValue(value)}
                </td>
              )
            })}
          </tr>
        ))}
      </tbody>
    </table>
  )
}
