import { latLngBounds } from 'leaflet'
import { useMemo } from 'react'
import { useTranslation } from 'react-i18next'
import { CircleMarker, Tooltip } from 'react-leaflet'
import type { BaseLayer, StopDwell } from '../api'
import type { Format } from '../i18n/format'
import { BaseMap } from '../map/BaseMap'
import { cn } from '../ui/cn'
import { LEGEND, SWATCH } from '../charts/marks'
import { EXCESS_BINS, excessStep } from './shared'

const STEP_CLASS = ['', 'bg-map-seq-1', 'bg-map-seq-2', 'bg-map-seq-3', 'bg-map-seq-4', 'bg-map-seq-5']

/** Radius grows with the square root of visits, so area tracks the count. */
function radiusFor(visits: number, maxVisits: number) {
  return 4 + 10 * Math.sqrt(visits / Math.max(1, maxVisits))
}

/**
 * Stops on the map, coloured by how much longer vehicles stand there than their passengers need
 * (one petrol ramp, fixed map ink) and sized by how often they stop. Clicking a stop opens its detail.
 */
export function DwellMap({
  layers,
  stops,
  selected,
  onSelect,
  format,
}: {
  layers: BaseLayer[]
  stops: StopDwell[]
  selected: number | null
  onSelect: (code: number) => void
  format: Format
}) {
  const { t } = useTranslation()
  const placed = useMemo(() => stops.filter((s): s is StopDwell & { latitude: number; longitude: number } => s.latitude !== null && s.longitude !== null), [stops])
  const maxVisits = Math.max(1, ...placed.map((s) => s.visits))
  const bounds = useMemo(() => latLngBounds(placed.map((s) => [s.latitude, s.longitude])), [placed])
  // Biggest extra drawn last, on top.
  const ordered = useMemo(() => [...placed].sort((a, b) => a.medianExcessSeconds - b.medianExcessSeconds), [placed])

  const legend = [
    `≤ ${format.seconds(EXCESS_BINS[0])}`,
    ...EXCESS_BINS.slice(1).map((upper, i) => `${format.seconds(EXCESS_BINS[i])} – ${format.seconds(upper)}`),
    `> ${format.seconds(EXCESS_BINS[EXCESS_BINS.length - 1])}`,
  ]

  return (
    <figure className="m-0 flex min-w-0 flex-col rounded-[10px] border border-rule bg-paper px-[18px] pt-4 pb-3.5">
      <figcaption className="mb-2.5">
        <h2 className="text-lg">{t('dwell.map.title')}</h2>
        <p className="mt-0.5 max-w-[72ch] text-sm text-ink-2">{t('dwell.map.subtitle')}</p>
      </figcaption>
      <div className="relative flex h-[420px] overflow-hidden rounded-lg border border-rule">
        {placed.length > 0 && (
          <BaseMap layers={layers} bounds={bounds}>
            {ordered.map((s) => {
              const step = excessStep(s.medianExcessSeconds)
              const isSelected = s.code === selected
              return (
                <CircleMarker
                  key={s.code}
                  center={[s.latitude, s.longitude]}
                  radius={radiusFor(s.visits, maxVisits)}
                  pathOptions={{
                    color: isSelected ? 'var(--map-route)' : 'var(--map-casing)',
                    weight: isSelected ? 3 : 1,
                    fillColor: `var(--map-seq-${step})`,
                    fillOpacity: 0.9,
                  }}
                  eventHandlers={{ click: () => onSelect(s.code) }}
                >
                  <Tooltip>
                    <strong>{s.name || s.code}</strong> <span className="text-ink-2">({s.code})</span>
                    <br />
                    {format.seconds(s.medianSeconds)} · {s.medianExcessSeconds > 0 ? '+' : ''}
                    {format.seconds(s.medianExcessSeconds)} · {format.number(s.visits)} {t('dwell.map.visits')}
                  </Tooltip>
                </CircleMarker>
              )
            })}
          </BaseMap>
        )}
      </div>
      <div className="mt-2.5 flex flex-wrap items-center gap-x-3 gap-y-1">
        <span className="text-xs font-semibold text-ink-2">{t('dwell.map.legend')}</span>
        <ul className={cn(LEGEND, 'm-0 list-none p-0')}>
          {legend.map((label, i) => (
            <li key={label}>
              <i className={cn(SWATCH, STEP_CLASS[i + 1], 'border border-rule')} aria-hidden="true" /> {label}
            </li>
          ))}
        </ul>
      </div>
      {placed.length < stops.length && <p className="mt-1 text-xs text-ink-2">{t('dwell.map.noPosition', { count: stops.length - placed.length })}</p>}
    </figure>
  )
}
