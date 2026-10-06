import { useEffect, useState } from 'react'
import { screenPath, type ScreenId } from './screens'

// Links between screens ride on the URL hash, which already names the screen: "#/statistiky/provoz?stop=163305"
// opens the dwell screen on that stop. The screen reads its parameters when it opens, then the hash
// goes back to just the screen, so reloading later doesn't bring back an old selection.

/** The screen's path in the hash ("mapa", "statistiky/provoz"), without its parameters. */
export function pathInHash() {
  return window.location.hash.replace(/^#\/?/, '').split('?')[0]
}

/** Open another screen with parameters, e.g. openScreen('provoz', { stop: 163305 }). */
export function openScreen(screen: ScreenId, params: Record<string, string | number | null | undefined> = {}) {
  const query = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) if (value !== null && value !== undefined) query.set(key, String(value))
  const qs = query.toString()
  window.location.hash = `/${screenPath(screen)}${qs ? `?${qs}` : ''}`
}

const readParams = () => new URLSearchParams(window.location.hash.split('?')[1] ?? '')

/**
 * The parameters a link opened this screen with, read once when it mounts. Reading stays pure (React
 * may run it twice); the hash is cleared after the first render.
 */
export function useScreenParams(): URLSearchParams {
  const [params] = useState(readParams)
  useEffect(() => {
    if (window.location.hash.includes('?')) window.history.replaceState(null, '', `#/${pathInHash()}`)
  }, [])
  return params
}

/** A number parameter, or null. */
export function numberParam(params: URLSearchParams, name: string) {
  const value = params.get(name)
  return value !== null && /^\d+$/.test(value) ? Number(value) : null
}
