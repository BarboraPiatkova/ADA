import { point, type LatLng, type Map as LeafletMap, type Point } from 'leaflet'
import { useState } from 'react'
import { useMap, useMapEvents } from 'react-leaflet'

// Which way a stop post serves, drawn the same on every map: an arrow in the direction of travel,
// and the posts of one station moved apart, each to its right-hand side (as on a network diagram).

/** How far (px) the directions of one station move apart. */
const DIRECTION_OFFSET = 7
/** Arrow length and half-width (px) beyond the marker. */
const ARROW_LENGTH = 7
const ARROW_HALF_WIDTH = 4

export interface DirectedStop {
  code: number
  latitude: number
  longitude: number
  /** Compass direction of travel (0 = north); null = unknown, drawn without an arrow. */
  bearing: number | null
}

export interface DirectedMarker<T> {
  stop: T
  center: LatLng
  /** The arrow's three corners, or null without a direction. */
  arrow: [LatLng, LatLng, LatLng] | null
}

/** Station = post code without its last two digits (the post). */
const stationOf = (code: number) => Math.floor(code / 100)

/**
 * Screen-space layout of directed stops at the map's current zoom: offsets and arrows are in pixels,
 * so call it again after every zoom (see useZoom).
 */
export function layoutDirections<T extends DirectedStop>(map: LeafletMap, stops: T[], radiusOf: (stop: T) => number): DirectedMarker<T>[] {
  const directed = new Map<number, number>()
  stops.forEach((s) => s.bearing !== null && directed.set(stationOf(s.code), (directed.get(stationOf(s.code)) ?? 0) + 1))

  return stops.map((stop) => {
    const base = map.latLngToLayerPoint([stop.latitude, stop.longitude])
    if (stop.bearing === null) return { stop, center: map.layerPointToLatLng(base), arrow: null }
    const rad = (stop.bearing * Math.PI) / 180
    // Screen y grows downwards: travel direction d, its right-hand side r = d turned 90° clockwise.
    const d = point(Math.sin(rad), -Math.cos(rad))
    const r = point(-d.y, d.x)
    const center = (directed.get(stationOf(stop.code)) ?? 0) > 1 ? base.add(r.multiplyBy(DIRECTION_OFFSET)) : base
    const radius = radiusOf(stop)
    const back = center.add(d.multiplyBy(radius - 1))
    const at = (p: Point) => map.layerPointToLatLng(p)
    return {
      stop,
      center: at(center),
      arrow: [at(center.add(d.multiplyBy(radius + ARROW_LENGTH))), at(back.add(r.multiplyBy(ARROW_HALF_WIDTH))), at(back.subtract(r.multiplyBy(ARROW_HALF_WIDTH)))],
    }
  })
}

/**
 * The map's zoom, and whether a zoom is under way. During the zoom animation Leaflet scales the arrows
 * (geographic shapes) while the markers keep their pixel size, so the arrows would stick out of their
 * circles until the zoom ends: callers hide them while `zooming`, and lay them out again at `zoom`.
 */
export function useZoom() {
  const map = useMap()
  const [state, setState] = useState(() => ({ zoom: map.getZoom(), zooming: false }))
  useMapEvents({
    zoomstart: () => setState((s) => ({ ...s, zooming: true })),
    zoomend: () => setState({ zoom: map.getZoom(), zooming: false }),
  })
  return state
}

const plain = (text: string) => text.normalize('NFD').replace(/\p{Diacritic}/gu, '').toLowerCase()

/** "Mendlovo namesti → Hlavni nadrazi": the stop post named with the direction it serves. */
export function stopLabel(stop: { name: string; code: number; toward: string | null } | undefined, code?: number) {
  if (!stop) return String(code ?? '')
  const name = stop.name || String(stop.code)
  return stop.toward && plain(stop.toward) !== plain(name) ? `${name} → ${stop.toward}` : name
}

/** Arrow style: route colour with a light casing, like the line diagram; never takes clicks. */
export const ARROW_STYLE = { color: 'var(--map-casing)', weight: 1, fillColor: 'var(--map-route)', fillOpacity: 1 }
