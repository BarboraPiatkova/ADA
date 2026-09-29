import { latLngBounds, type LatLngBounds } from 'leaflet'
import { Fragment, useEffect, useMemo, useState, type ReactNode } from 'react'
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
  // Enlarged, the same map (same view, same zoom) fills the window; Escape or the button shrinks it back.
  const [expanded, setExpanded] = useState(false)
  useEffect(() => {
    if (!expanded) return
    const close = (event: KeyboardEvent) => event.key === 'Escape' && setExpanded(false)
    const overflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    window.addEventListener('keydown', close)
    return () => {
      document.body.style.overflow = overflow
      window.removeEventListener('keydown', close)
    }
  }, [expanded])

  return (
    <figure
      className={cn(
        'm-0 flex min-w-0 flex-col border-rule bg-paper px-[18px] pt-4 pb-3.5',
        expanded ? 'fixed inset-0 z-[900]' : 'rounded-[10px] border',
      )}
    >
      <figcaption className="mb-2.5 flex items-start justify-between gap-4">
        <div>
          <h2 className="text-lg">{title}</h2>
          <p className="mt-0.5 max-w-[72ch] text-sm text-ink-2">{subtitle}</p>
        </div>
        <button
          className="inline-flex h-8 shrink-0 cursor-pointer items-center gap-1.5 touch-target rounded-lg border border-rule bg-paper px-2.5 text-sm hover:border-ink-2"
          aria-pressed={expanded}
          onClick={() => setExpanded((e) => !e)}
        >
          <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
            {expanded ? <path d="M9 4v5H4M15 4v5h5M9 20v-5H4M15 20v-5h5" /> : <path d="M4 9V4h5M20 9V4h-5M4 15v5h5M20 15v5h-5" />}
          </svg>
          {t(expanded ? 'dwell.map.shrink' : 'dwell.map.expand')}
        </button>
      </figcaption>
      <div className={cn('relative flex overflow-hidden rounded-lg border border-rule', expanded ? 'min-h-0 flex-1' : 'h-[420px]')}>
        {placed.length > 0 && (
          <BaseMap layers={layers} bounds={bounds}>
            <FitSize expanded={expanded} bounds={bounds} />
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

/** Leaflet measures its box once; after enlarging or shrinking it measures again and fits the stops to the new size. */
function FitSize({ expanded, bounds }: { expanded: boolean; bounds: LatLngBounds }) {
  const map = useMap()
  useEffect(() => {
    map.invalidateSize()
    map.fitBounds(bounds, { padding: [24, 24] })
    // eslint-disable-next-line react-hooks/exhaustive-deps -- only on a size change, not when the data refreshes
  }, [map, expanded])
  return null
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
