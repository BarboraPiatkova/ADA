import { useQuery } from '@tanstack/react-query'
import { latLngBounds } from 'leaflet'
import { Accordion } from 'radix-ui'
import { Fragment, useEffect, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { CircleMarker, Polyline, Popup, Tooltip, useMap } from 'react-leaflet'
import type { BaseLayer, Line, PatternSummary, Stop } from '../api'
import { linesQuery, patternStopsQuery, stopsQuery } from '../queries'
import { cn } from '../ui/cn'
import { Empty } from '../ui/Empty'
import { Chevron } from '../ui/icons'
import { QueryState } from '../ui/QueryState'
import { useCoarsePointer } from '../ui/useCoarsePointer'
import { BaseMap } from './BaseMap'
import { LinePickerSkeleton, MapSkeleton } from './MapSkeleton'

/** Marker radius grows with the square root of mean boardings, so area tracks volume. */
function radiusFor(stop: Stop) {
  if (stop.visits === 0) return 3
  return Math.min(4 + Math.sqrt(stop.boardings / stop.visits) * 2.5, 14)
}

/** A stop marker as drawn on the map: white fill, route-coloured ring. */
const LEGEND_STOP = 'inline-block rounded-full border-2 border-map-route bg-map-stop'

function FitTo({ points }: { points: [number, number][] }) {
  const map = useMap()
  useEffect(() => {
    if (points.length > 1) map.fitBounds(latLngBounds(points), { padding: [40, 40] })
  }, [map, points])
  return null
}

/** Terminus names come as "14901 Purmerendská"; the leading stop code means nothing to a reader. */
const withoutStopCode = (name: string | null) => name?.replace(/^\d+\s+/, '') ?? '?'

function patternLabel(p: PatternSummary) {
  return `${withoutStopCode(p.firstStopName)} → ${withoutStopCode(p.lastStopName)}`
}

function LinePicker({ lines, selected, onSelect }: { lines: Line[]; selected: number | null; onSelect: (code: number | null) => void }) {
  const { t } = useTranslation()
  return (
    <nav
      className="max-h-[35svh] shrink-0 overflow-y-auto border-b border-rule bg-paper px-3.5 py-[18px] md:max-h-none md:w-[300px] md:border-r md:border-b-0"
      aria-label={t('map.lines')}
    >
      <h2 className="mb-1 px-1.5 text-xl">{t('map.lines')}</h2>
      <p className="mb-3 px-1.5 text-sm text-ink-2">{t('map.linesHint')}</p>
      <Accordion.Root type="single" collapsible className="flex flex-col gap-0.5">
        {lines.map((line) => {
          const withTrips = line.patterns.filter((p) => p.trips > 0)
          const hidden = line.patterns.length - withTrips.length
          return (
            <Accordion.Item key={line.id} value={String(line.id)}>
              <Accordion.Header asChild>
                <h3 className="font-sans text-base font-normal">
                  <Accordion.Trigger className="group flex w-full cursor-pointer items-center gap-3 rounded-lg px-1.5 py-[7px] text-left touch-target hover:bg-surface">
                    <span className="min-w-[46px] rounded-md bg-route px-2 py-[3px] text-center font-display text-lg leading-[1.1] font-bold text-on-route">{line.id}</span>
                    <span className="text-sm text-ink-2">{t('map.patternsWithTrips', { count: withTrips.length })}</span>
                    <span className="ml-auto text-ink-2 transition-transform duration-150 group-data-[state=open]:rotate-180">
                      <Chevron />
                    </span>
                  </Accordion.Trigger>
                </h3>
              </Accordion.Header>
              <Accordion.Content className="mt-0.5 mb-2 ml-[29px] border-l-[3px] border-route-soft pl-3.5">
                {withTrips.map((p) => (
                  <button
                    key={p.code}
                    className="flex w-full cursor-pointer flex-col rounded-md px-2 py-1.5 text-left hover:bg-surface aria-pressed:bg-route-soft aria-pressed:shadow-[inset_3px_0_0_var(--route)]"
                    aria-pressed={selected === p.code}
                    onClick={() => onSelect(selected === p.code ? null : p.code)}
                  >
                    <span className="font-medium">{patternLabel(p)}</span>
                    <span className="text-xs text-ink-2">
                      {t('map.patternMeta', { trips: t('map.trips', { count: p.trips }), stops: t('map.stops', { count: p.stopCount }) })}
                    </span>
                  </button>
                ))}
                {hidden > 0 && <p className="mx-2 mt-1 text-xs text-ink-2">{t('map.hiddenPatterns', { count: hidden })}</p>}
              </Accordion.Content>
            </Accordion.Item>
          )
        })}
      </Accordion.Root>
    </nav>
  )
}

/** Radius of the invisible tap area around a stop on touch screens: a 24px target. */
const TOUCH_HIT_RADIUS = 12
// Invisible, but a transparent fill still receives pointer events. A constant, so
// react-leaflet doesn't restyle every hit circle on each render.
const TOUCH_HIT_STYLE = { stroke: false, fillColor: '#000', fillOpacity: 0 }

function StopsLayer({ stops, onPattern }: { stops: Stop[]; onPattern: Set<number> }) {
  const { t } = useTranslation()
  const coarse = useCoarsePointer()
  return stops.map((s) => {
    const highlighted = onPattern.has(s.code)
    const dimmed = onPattern.size > 0 && !highlighted
    const radius = radiusFor(s)
    const details = (
      <>
        <strong>{s.name}</strong> <span className="text-ink-2">({s.code})</span>
        <br />
        {s.visits === 0
          ? t('map.noVisits')
          : t('map.stopActivity', { count: s.visits, boardings: s.boardings / s.visits, alightings: s.alightings / s.visits })}
      </>
    )
    return (
      <Fragment key={s.code}>
        <CircleMarker
          center={[s.latitude, s.longitude]}
          radius={radius}
          interactive={!coarse}
          pathOptions={{
            color: s.visits === 0 ? 'var(--map-no-data)' : 'var(--map-route)',
            weight: highlighted ? 3.5 : 2,
            fillColor: 'var(--map-stop)',
            fillOpacity: dimmed ? 0.5 : 0.95,
            opacity: dimmed ? 0.3 : s.visits === 0 ? 0.6 : 1,
          }}
        >
          {!coarse && <Tooltip>{details}</Tooltip>}
        </CircleMarker>
        {/* Touch: a finger can't hit a 4px circle, so a larger invisible one takes the tap.
            A tap has no hover, so the details open as a popup that stays until dismissed. */}
        {coarse && (
          <CircleMarker center={[s.latitude, s.longitude]} radius={Math.max(TOUCH_HIT_RADIUS, radius)} pathOptions={TOUCH_HIT_STYLE}>
            <Popup closeButton autoPan>
              {details}
            </Popup>
          </CircleMarker>
        )}
      </Fragment>
    )
  })
}

export function NetworkMapView({ baseLayers }: { baseLayers: BaseLayer[] }) {
  const { t } = useTranslation()
  const stops = useQuery(stopsQuery)
  const lines = useQuery(linesQuery)
  const [selected, setSelected] = useState<number | null>(null)
  const pattern = useQuery({ ...patternStopsQuery(selected ?? 0), enabled: selected !== null })

  const patternData = selected !== null ? pattern.data : undefined
  const patternPoints = useMemo<[number, number][]>(
    () =>
      (patternData ?? []).flatMap((s) => (s.latitude !== null && s.longitude !== null ? [[s.latitude, s.longitude] as [number, number]] : [])),
    [patternData],
  )
  const onPattern = useMemo(() => new Set((patternData ?? []).map((s) => s.code)), [patternData])

  return (
    <>
      {/* The map has no visible title (the tab names it); screen readers still get one. */}
      <h1 className="sr-only">{t('app.views.mapa')}</h1>
      <QueryState query={stops} loading={t('map.loadingStops')} skeleton={<MapSkeleton label={t('map.loadingStops')} />}>
        {(stopList) =>
          stopList.length === 0 ? (
            <Empty>{t('map.noStops')}</Empty>
          ) : (
            <div className="flex min-w-0 flex-1 flex-col md:flex-row">
              <QueryState query={lines} loading={t('map.loadingLines')} skeleton={<LinePickerSkeleton label={t('map.loadingLines')} />}>
                {(lineList) => <LinePicker lines={lineList} selected={selected} onSelect={setSelected} />}
              </QueryState>
              <div className="relative flex min-w-0 flex-1">
                <BaseMap layers={baseLayers} bounds={latLngBounds(stopList.map((s) => [s.latitude, s.longitude]))}>
                  {patternPoints.length > 1 && (
                    <>
                      {/* Drawn like a line diagram: a light casing under the route colour. */}
                      <Polyline positions={patternPoints} pathOptions={{ color: 'var(--map-casing)', weight: 10, opacity: 0.9 }} />
                      <Polyline positions={patternPoints} pathOptions={{ color: 'var(--map-route)', weight: 5 }} />
                      <FitTo points={patternPoints} />
                    </>
                  )}
                  <StopsLayer stops={stopList} onPattern={onPattern} />
                </BaseMap>
                <div
                  className="absolute right-3 bottom-[26px] z-[500] flex items-center gap-3.5 rounded-lg bg-paper px-3 py-1.5 text-xs shadow-float [&>span]:inline-flex [&>span]:items-center [&>span]:gap-1.5"
                  aria-hidden="true"
                >
                  <span>
                    <i className={cn(LEGEND_STOP, 'size-[9px]')} /> {t('map.legendFewer')}
                  </span>
                  <span>
                    <i className={cn(LEGEND_STOP, 'size-[17px]')} /> {t('map.legendMore')}
                  </span>
                  <span>
                    <i className={cn(LEGEND_STOP, 'size-[9px] border-map-no-data')} /> {t('map.legendNoData')}
                  </span>
                </div>
              </div>
            </div>
          )
        }
      </QueryState>
    </>
  )
}
