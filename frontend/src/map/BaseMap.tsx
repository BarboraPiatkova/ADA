import 'leaflet/dist/leaflet.css'
import { Control, DomUtil, type LatLngBoundsExpression } from 'leaflet'
import { useEffect, useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { LayersControl, MapContainer, TileLayer, useMap, useMapEvents } from 'react-leaflet'
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

function BaseLayerTracker({ layers, nameOf, onChange }: { layers: BaseLayer[]; nameOf: (layer: BaseLayer) => string; onChange: (layer: BaseLayer) => void }) {
  useMapEvents({
    baselayerchange: (event) => {
      const layer = layers.find((l) => nameOf(l) === event.name)
      if (layer) onChange(layer)
    },
  })
  return null
}

/**
 * A Leaflet map with the deployment's base layers (from /api/map/config) in a layer
 * switcher. The chosen layer is remembered per browser.
 */
export function BaseMap({ layers, bounds, children }: { layers: BaseLayer[]; bounds: LatLngBoundsExpression; children?: ReactNode }) {
  const { t, i18n } = useTranslation()
  const [active, setActive] = useState<BaseLayer | undefined>(() => layers.find((l) => l.id === rememberedLayer()) ?? layers[0])
  // Unknown ids (a layer added to the API later) show their id rather than nothing.
  const nameOf = (layer: BaseLayer) => t(`map.layers.${layer.id}` as 'map.layers.osm', { defaultValue: layer.id })

  return (
    <MapContainer bounds={bounds} boundsOptions={{ padding: [24, 24] }} className="map">
      {/* Re-created on language change: Leaflet's control keeps the labels it was built with. */}
      <LayersControl key={i18n.resolvedLanguage} position="topright">
        {layers.map((layer) => (
          <LayersControl.BaseLayer key={layer.id} name={nameOf(layer)} checked={layer.id === active?.id}>
            <TileLayer url={layer.url} attribution={layer.attribution} maxZoom={layer.maxZoom} />
          </LayersControl.BaseLayer>
        ))}
      </LayersControl>
      <BaseLayerTracker
        layers={layers}
        nameOf={nameOf}
        onChange={(layer) => {
          setActive(layer)
          rememberLayer(layer.id)
        }}
      />
      {active?.requiresMapyLogo && <MapyLogo />}
      {children}
    </MapContainer>
  )
}
