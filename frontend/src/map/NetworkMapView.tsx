import { useQuery } from '@tanstack/react-query'
import { latLngBounds } from 'leaflet'
import { Accordion } from 'radix-ui'
import { useEffect, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { CircleMarker, Polyline, Tooltip, useMap } from 'react-leaflet'
import type { BaseLayer, Line, PatternSummary, Stop } from '../api'
import { linesQuery, patternStopsQuery, stopsQuery } from '../queries'
import { Chevron } from '../ui/icons'
import { QueryState } from '../ui/QueryState'
import { BaseMap } from './BaseMap'

/** Marker radius grows with the square root of mean boardings, so area tracks volume. */
function radiusFor(stop: Stop) {
  if (stop.visits === 0) return 3
  return Math.min(4 + Math.sqrt(stop.boardings / stop.visits) * 2.5, 14)
}

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
    <nav className="sidebar" aria-label={t('map.lines')}>
      <h2>{t('map.lines')}</h2>
      <p className="sidebar-hint">{t('map.linesHint')}</p>
      <Accordion.Root type="single" collapsible className="line-list">
        {lines.map((line) => {
          const withTrips = line.patterns.filter((p) => p.trips > 0)
          const hidden = line.patterns.length - withTrips.length
          return (
            <Accordion.Item key={line.id} value={String(line.id)} className="line-item">
              <Accordion.Header asChild>
                <h3 className="line-header">
                  <Accordion.Trigger className="line-button">
                    <span className="line-badge">{line.id}</span>
                    <span className="line-meta">{t('map.patternsWithTrips', { count: withTrips.length })}</span>
                    <span className="chevron">
                      <Chevron />
                    </span>
                  </Accordion.Trigger>
                </h3>
              </Accordion.Header>
              <Accordion.Content className="pattern-list">
                {withTrips.map((p) => (
                  <button
                    key={p.code}
                    className="pattern-button"
                    aria-pressed={selected === p.code}
                    onClick={() => onSelect(selected === p.code ? null : p.code)}
                  >
                    <span className="pattern-name">{patternLabel(p)}</span>
                    <span className="muted small">
                      {t('map.patternMeta', { trips: t('map.trips', { count: p.trips }), stops: t('map.stops', { count: p.stopCount }) })}
                    </span>
                  </button>
                ))}
                {hidden > 0 && <p className="muted small pattern-hidden">{t('map.hiddenPatterns', { count: hidden })}</p>}
              </Accordion.Content>
            </Accordion.Item>
          )
        })}
      </Accordion.Root>
    </nav>
  )
}

function StopsLayer({ stops, onPattern }: { stops: Stop[]; onPattern: Set<number> }) {
  const { t } = useTranslation()
  return stops.map((s) => {
    const highlighted = onPattern.has(s.code)
    const dimmed = onPattern.size > 0 && !highlighted
    return (
      <CircleMarker
        key={s.code}
        center={[s.latitude, s.longitude]}
        radius={radiusFor(s)}
        pathOptions={{
          color: s.visits === 0 ? 'var(--map-no-data)' : 'var(--map-route)',
          weight: highlighted ? 3.5 : 2,
          fillColor: 'var(--map-stop)',
          fillOpacity: dimmed ? 0.5 : 0.95,
          opacity: dimmed ? 0.3 : s.visits === 0 ? 0.6 : 1,
        }}
      >
        <Tooltip>
          <strong>{s.name}</strong> <span className="muted">({s.code})</span>
          <br />
          {s.visits === 0
            ? t('map.noVisits')
            : t('map.stopActivity', { count: s.visits, boardings: s.boardings / s.visits, alightings: s.alightings / s.visits })}
        </Tooltip>
      </CircleMarker>
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
    <QueryState query={stops} loading={t('map.loadingStops')}>
      {(stopList) =>
        stopList.length === 0 ? (
          <p className="empty">{t('map.noStops')}</p>
        ) : (
          <div className="split">
            <QueryState query={lines} loading={t('map.loadingLines')}>
              {(lineList) => <LinePicker lines={lineList} selected={selected} onSelect={setSelected} />}
            </QueryState>
            <div className="map-wrap">
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
              <div className="map-legend" aria-hidden="true">
                <span>
                  <i className="legend-stop small" /> {t('map.legendFewer')}
                </span>
                <span>
                  <i className="legend-stop big" /> {t('map.legendMore')}
                </span>
                <span>
                  <i className="legend-stop none" /> {t('map.legendNoData')}
                </span>
              </div>
            </div>
          </div>
        )
      }
    </QueryState>
  )
}
