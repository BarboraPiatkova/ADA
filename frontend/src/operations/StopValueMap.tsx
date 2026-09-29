import { latLngBounds } from 'leaflet'
import { Fragment, useMemo, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { CircleMarker, Polygon, Tooltip, useMap } from 'react-leaflet'
import type { BaseLayer } from '../api'
import { BaseMap } from '../map/BaseMap'
import { ARROW_STYLE, layoutDirections, stopLabel, useZoom } from '../map/directions'
import { cn } from '../ui/cn'
import { LEGEND, SWATCH } from '../charts/marks'

const STEP_CLASS = ['', 'bg-map-seq-1', 'bg-map-seq-2', 'bg-map-seq-3', 'bg-map-seq-4', 'bg-map-seq-5']

/** A stop post drawn on a value map. */
export interface ValueStop {
  code: number
  name: string
  toward: string | null
  bearing: number | null
  latitude: number | null
  longitude: number | null
  /** How big the circle is, e.g. visits or departures. */
  size: number
  /** 1–5: which step of the petrol ramp the stop's value falls in. */
  step: number
  /** The value line in the tooltip. */
  detail: string
}

type Placed = ValueStop & { latitude: number; longitude: number }

/** Radius grows with the square root of the size, so area tracks the count. */
const radiusFor = (size: number, max: number) => 4 + 10 * Math.sqrt(size / Math.max(1, max))

/**
 * Stops on the map, coloured by one value on the petrol ramp (fixed map ink) and sized by a count, each
 * post with an arrow in its direction of travel. Clicking a stop calls onSelect.
 */
export function StopValueMap({
  layers,
  stops,
  selected,
  onSelect,
  title,
  subtitle,
  legendTitle,
  legendLabels,
  notes,
}: {
  layers: BaseLayer[]
  stops: ValueStop[]
  selected?: number | null
  onSelect?: (code: number) => void
  title: string
  subtitle: string
  legendTitle: string
  /** Five labels, lightest step first. */
  legendLabels: string[]
  notes?: ReactNode
}) {
  const { t } = useTranslation()
  const placed = useMemo(() => stops.filter((s): s is Placed => s.latitude !== null && s.longitude !== null), [stops])
  const bounds = useMemo(() => latLngBounds(placed.map((s) => [s.latitude, s.longitude])), [placed])

  return (
    <figure className="m-0 flex min-w-0 flex-col rounded-[10px] border border-rule bg-paper px-[18px] pt-4 pb-3.5">
      <figcaption className="mb-2.5">
        <h2 className="text-lg">{title}</h2>
        <p className="mt-0.5 max-w-[72ch] text-sm text-ink-2">{subtitle}</p>
      </figcaption>
      <div className="relative flex h-[420px] overflow-hidden rounded-lg border border-rule">
        {placed.length > 0 && (
          <BaseMap layers={layers} bounds={bounds}>
            <Markers stops={placed} selected={selected ?? null} onSelect={onSelect} />
          </BaseMap>
        )}
      </div>
      <div className="mt-2.5 flex flex-wrap items-center gap-x-3 gap-y-1">
        <span className="text-xs font-semibold text-ink-2">{legendTitle}</span>
        <ul className={cn(LEGEND, 'm-0 list-none p-0')}>
          {legendLabels.map((label, i) => (
            <li key={label}>
              <i className={cn(SWATCH, STEP_CLASS[i + 1], 'border border-rule')} aria-hidden="true" /> {label}
            </li>
          ))}
        </ul>
      </div>
      <p className="mt-1 text-xs text-ink-2">{t('dwell.map.direction')}</p>
      {notes}
      {placed.length < stops.length && <p className="mt-1 text-xs text-ink-2">{t('dwell.map.noPosition', { count: stops.length - placed.length })}</p>}
    </figure>
  )
}

function Markers({ stops, selected, onSelect }: { stops: Placed[]; selected: number | null; onSelect?: (code: number) => void }) {
  const map = useMap()
  const { zoom, zooming } = useZoom()
  const maxSize = Math.max(1, ...stops.map((s) => s.size))
  const radius = (s: Placed) => radiusFor(s.size, maxSize)
  // Darkest drawn last, on top.
  const markers = useMemo(
    () => layoutDirections(map, [...stops].sort((a, b) => a.step - b.step), radius),
    // eslint-disable-next-line react-hooks/exhaustive-deps -- layer points change with the zoom
    [map, stops, zoom],
  )

  return markers.map(({ stop, center, arrow }) => {
    const isSelected = stop.code === selected
    return (
      <Fragment key={stop.code}>
        {arrow && !zooming && <Polygon positions={arrow} interactive={false} pathOptions={ARROW_STYLE} />}
        <CircleMarker
          center={center}
          radius={radius(stop)}
          pathOptions={{
            color: isSelected ? 'var(--map-route)' : 'var(--map-casing)',
            weight: isSelected ? 3 : 1,
            fillColor: `var(--map-seq-${stop.step})`,
            fillOpacity: 0.9,
          }}
          eventHandlers={onSelect ? { click: () => onSelect(stop.code) } : undefined}
        >
          <Tooltip>
            <strong>{stopLabel(stop)}</strong> <span className="text-ink-2">({stop.code})</span>
            <br />
            {stop.detail}
          </Tooltip>
        </CircleMarker>
      </Fragment>
    )
  })
}
