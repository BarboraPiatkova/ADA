import 'leaflet/dist/leaflet.css'
import { Control, DomEvent, DomUtil, type LatLngBoundsExpression } from 'leaflet'
import { Popover } from 'radix-ui'
import { useEffect, useRef, useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { MapContainer, TileLayer, useMap, useMapEvents } from 'react-leaflet'
import type { BaseLayer } from '../api'

const STORAGE_KEY = 'adaplatform.baseLayer'

function rememberedLayer(): string | null {
  try {
    return localStorage.getItem(STORAGE_KEY)
  } catch {
    return null
  }
}

function rememberLayer(id: string) {
  try {
    localStorage.setItem(STORAGE_KEY, id)
  } catch {
    // Storage blocked (private window etc.) — the choice just isn't remembered.
  }
}

/** Mapy.com's terms require its logo on the map while its tiles are displayed. */
function MapyLogo() {
  const map = useMap()
  useEffect(() => {
    const logo = new Control({ position: 'bottomleft' })
    logo.onAdd = () => {
      const link = DomUtil.create('a', 'mapy-logo')
      link.href = 'https://mapy.com/'
      link.target = '_blank'
      link.rel = 'noopener'
      link.innerHTML = '<img src="https://api.mapy.com/img/api/logo.svg" alt="Mapy.com" width="100">'
      return link
    }
    logo.addTo(map)
    return () => {
      logo.remove()
    }
  }, [map])
  return null
}

/**
 * URL of the single tile under the map's centre, one zoom level out — a live preview of
 * how a layer would show the area being looked at.
 */
function previewUrl(layer: BaseLayer, lat: number, lng: number, zoom: number) {
  const z = Math.min(Math.max(Math.round(zoom) - 1, 3), Math.min(layer.maxZoom, 16))
  const n = 2 ** z
  const x = Math.floor(((lng + 180) / 360) * n)
  const latRad = (lat * Math.PI) / 180
  const y = Math.floor(((1 - Math.log(Math.tan(latRad) + 1 / Math.cos(latRad)) / Math.PI) / 2) * n)
  return layer.url.replace('{z}', String(z)).replace('{x}', String(x)).replace('{y}', String(y)).replace('{r}', '')
}

function LayerPicker({ layers, active, onChange }: { layers: BaseLayer[]; active: BaseLayer; onChange: (layer: BaseLayer) => void }) {
  const { t } = useTranslation()
  const map = useMap()
  const [view, setView] = useState(() => ({ center: map.getCenter(), zoom: map.getZoom() }))
  const [open, setOpen] = useState(false)
  const wrapper = useRef<HTMLDivElement>(null)

  useMapEvents({ moveend: () => setView({ center: map.getCenter(), zoom: map.getZoom() }) })

  // Clicks and scrolls on the picker must not pan or zoom the map underneath.
  useEffect(() => {
    if (!wrapper.current) return
    DomEvent.disableClickPropagation(wrapper.current)
    DomEvent.disableScrollPropagation(wrapper.current)
  }, [])

  const nameOf = (layer: BaseLayer) => t(`map.layers.${layer.id}` as 'map.layers.osm', { defaultValue: layer.id })
  const preview = (layer: BaseLayer) => previewUrl(layer, view.center.lat, view.center.lng, view.zoom)

  return (
    <div ref={wrapper} className="layer-picker">
      <Popover.Root open={open} onOpenChange={setOpen}>
        <Popover.Trigger className="layer-trigger" aria-label={t('map.chooseBaseMap')} title={t('map.chooseBaseMap')}>
          <img src={preview(active)} alt="" className="layer-thumb" />
          <span className="layer-trigger-label">
            <svg viewBox="0 0 24 24" width="12" height="12" fill="none" stroke="currentColor" strokeWidth="2" strokeLinejoin="round" aria-hidden="true">
              <path d="M12 3 2 8l10 5 10-5-10-5zM2 13l10 5 10-5M2 18l10 5 10-5" />
            </svg>
            {nameOf(active)}
          </span>
        </Popover.Trigger>
        <Popover.Portal>
          <Popover.Content className="layer-panel" side="bottom" align="end" sideOffset={8} collisionPadding={12}>
            <p className="layer-panel-title">{t('map.chooseBaseMap')}</p>
            <div className="layer-grid" role="radiogroup" aria-label={t('map.chooseBaseMap')}>
              {layers.map((layer) => {
                const selected = layer.id === active.id
                return (
                  <button
                    key={layer.id}
                    role="radio"
                    aria-checked={selected}
                    className={`layer-option${selected ? ' selected' : ''}`}
                    onClick={() => {
                      onChange(layer)
                      setOpen(false)
                    }}
                  >
                    <img src={preview(layer)} alt="" className="layer-thumb" loading="lazy" />
                    <span className="layer-option-name">{nameOf(layer)}</span>
                    {layer.requiresMapyLogo && <span className="layer-option-provider">Mapy.com</span>}
                  </button>
                )
              })}
            </div>
          </Popover.Content>
        </Popover.Portal>
      </Popover.Root>
    </div>
  )
}

/**
 * A Leaflet map with the deployment's base layers (from /api/map/config) and a picker
 * showing a live preview of each. The chosen layer is remembered per browser.
 */
export function BaseMap({ layers, bounds, children }: { layers: BaseLayer[]; bounds: LatLngBoundsExpression; children?: ReactNode }) {
  const [activeId, setActiveId] = useState(() => layers.find((l) => l.id === rememberedLayer())?.id ?? layers[0]?.id)
  const active = layers.find((l) => l.id === activeId) ?? layers[0]

  return (
    <MapContainer bounds={bounds} boundsOptions={{ padding: [24, 24] }} className="map">
      {active && (
        <>
          {/* Keyed by id so switching swaps the tile layer (and its attribution) cleanly. */}
          <TileLayer key={active.id} url={active.url} attribution={active.attribution} maxZoom={active.maxZoom} />
          {active.requiresMapyLogo && <MapyLogo />}
          {layers.length > 1 && (
            <LayerPicker
              layers={layers}
              active={active}
              onChange={(layer) => {
                setActiveId(layer.id)
                rememberLayer(layer.id)
              }}
            />
          )}
        </>
      )}
      {children}
    </MapContainer>
  )
}
