import 'leaflet/dist/leaflet.css'
import { Control, DomEvent, DomUtil, type LatLngBoundsExpression } from 'leaflet'
import { Popover } from 'radix-ui'
import { useEffect, useRef, useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { MapContainer, TileLayer, useMap, useMapEvents } from 'react-leaflet'
import { cn } from '../ui/cn'
import { readSetting, writeSetting } from '../ui/storage'
import type { BaseLayer } from '../api'

const STORAGE_KEY = 'adaplatform.baseLayer'

// A city's network is a speck below this; zoom 7 still shows the whole Czech Republic.
const MIN_ZOOM = 7

const THUMB = 'block aspect-square w-full rounded-md bg-surface-2 object-cover'

const rememberedLayer = () => readSetting(STORAGE_KEY)
const rememberLayer = (id: string) => writeSetting(STORAGE_KEY, id)

/** Mapy.com's terms require its logo on the map while its tiles are displayed. */
function MapyLogo() {
  const map = useMap()
  useEffect(() => {
    const logo = new Control({ position: 'bottomleft' })
    logo.onAdd = () => {
      const link = DomUtil.create('a')
      link.href = 'https://mapy.com/'
      link.target = '_blank'
      link.rel = 'noopener'
      // Built with DOM calls, not innerHTML, so no markup is ever parsed from a string.
      const img = DomUtil.create('img', 'block', link)
      img.src = 'https://api.mapy.com/img/api/logo.svg'
      img.alt = 'Mapy.com'
      img.width = 100
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
    <div ref={wrapper} className="absolute top-3 right-3 z-[1000]">
      <Popover.Root open={open} onOpenChange={setOpen}>
        <Popover.Trigger
          className="block w-28 cursor-pointer rounded-[10px] bg-paper p-1 text-left shadow-float hover:shadow-[0_0_0_2px_var(--route),var(--shadow-float)] data-[state=open]:shadow-[0_0_0_2px_var(--route),var(--shadow-float)]"
          aria-label={t('map.chooseBaseMap')} title={t('map.chooseBaseMap')}>
          <img src={preview(active)} alt="" className={THUMB} />
          <span className="flex items-center gap-1 overflow-hidden px-0.5 pt-[5px] pb-px font-display text-sm font-semibold text-ellipsis whitespace-nowrap [&>svg]:shrink-0 [&>svg]:text-route">
            <svg viewBox="0 0 24 24" width="12" height="12" fill="none" stroke="currentColor" strokeWidth="2" strokeLinejoin="round" aria-hidden="true">
              <path d="M12 3 2 8l10 5 10-5-10-5zM2 13l10 5 10-5M2 18l10 5 10-5" />
            </svg>
            {nameOf(active)}
          </span>
        </Popover.Trigger>
        <Popover.Portal>
          <Popover.Content
            className="z-[1001] w-[min(348px,calc(100vw-24px))] rounded-xl border border-rule bg-paper p-3.5 shadow-float"
            side="bottom" align="end" sideOffset={8} collisionPadding={12}>
            <p className="mb-3 font-display text-lg font-semibold">{t('map.chooseBaseMap')}</p>
            <div className="grid grid-cols-3 gap-2.5" role="radiogroup" aria-label={t('map.chooseBaseMap')}>
              {layers.map((layer) => {
                const selected = layer.id === active.id
                return (
                  <button
                    key={layer.id}
                    role="radio"
                    aria-checked={selected}
                    className={cn(
                      'flex cursor-pointer flex-col gap-1 rounded-[10px] border-2 border-transparent p-1 text-left hover:bg-surface',
                      selected && 'border-route bg-route-soft',
                    )}
                    onClick={() => {
                      onChange(layer)
                      setOpen(false)
                    }}
                  >
                    <img src={preview(layer)} alt="" className={THUMB} loading="lazy" />
                    <span className="text-sm leading-tight font-semibold">{nameOf(layer)}</span>
                    {layer.requiresMapyLogo && <span className="-mt-[3px] text-xs text-ink-2">Mapy.com</span>}
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
    <MapContainer bounds={bounds} boundsOptions={{ padding: [24, 24] }} minZoom={MIN_ZOOM} className="flex-1 bg-surface font-sans">
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
