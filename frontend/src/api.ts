// Typed access to the AdaPlatform API. Paths are relative: the Vite dev proxy (and,
// in production, the API serving the SPA) makes them same-origin.

export interface BaseLayer {
  /** Stable id (e.g. "mapy-basic", "osm"); the UI names layers by id in its own language. */
  id: string
  /** Leaflet URL template; Mapy.com layers point at the API's caching tile proxy. */
  url: string
  attribution: string
  maxZoom: number
  /** Mapy.com's terms require its logo on the map while its tiles are shown. */
  requiresMapyLogo: boolean
}

export interface MapConfig {
  baseLayers: BaseLayer[]
}

export interface Stop {
  code: number
  name: string
  latitude: number
  longitude: number
  visits: number
  boardings: number
  alightings: number
}

export interface PatternSummary {
  code: number
  firstStopName: string | null
  lastStopName: string | null
  stopCount: number
  trips: number
}

export interface Line {
  id: number
  patterns: PatternSummary[]
}

export interface PatternStop {
  sequence: number
  code: number
  name: string
  latitude: number | null
  longitude: number | null
}

export type HealthStatus = 'Ok' | 'Warning' | 'Fault' | 'Unknown'

export type HealthReasonCode =
  | 'DeviceSilent'
  | 'DeviceFlagged'
  | 'DeviceNotAlive'
  | 'AllDevicesSilent'
  | 'SomeDevicesSilent'
  | 'Imbalance'
  | 'NegativeOccupancy'
  | 'FlaggedStops'
  | 'DeviceWarning'

/** Why a status was given: a code and the measured value (a count or a 0–1 share). */
export interface HealthReason {
  code: HealthReasonCode
  value: number | null
}

export interface DeviceHealth {
  deviceNumber: number
  firmwareVersion: string | null
  stopsCounted: number
  boardings: number
  alightings: number
  heartbeats: number
  notAliveHeartbeats: number
  restarts: number
  flaggedStops: number
  status: HealthStatus
  reasons: HealthReason[]
}

export interface VehicleHealth {
  vehicleId: number
  traction: string | null
  model: string | null
  days: number
  boardings: number
  alightings: number
  imbalance: number | null
  stopSummaries: number
  negativeOccupancyShare: number | null
  flaggedStopShare: number | null
  silentDevices: number
  status: HealthStatus
  reasons: HealthReason[]
  devices: DeviceHealth[]
}

export interface HealthThresholds {
  minPassengersForBalance: number
  imbalanceWarning: number
  imbalanceFault: number
  negativeOccupancyWarning: number
  negativeOccupancyFault: number
  flaggedStopsWarning: number
  notAliveWarning: number
}

export interface DeviceHealthReport {
  from: string | null
  to: string | null
  thresholds: HealthThresholds
  vehicles: VehicleHealth[]
}

async function getJson<T>(path: string, signal?: AbortSignal): Promise<T> {
  const response = await fetch(path, { signal, headers: { Accept: 'application/json' } })
  if (!response.ok) {
    throw new Error(`${path} → HTTP ${response.status}`)
  }
  return (await response.json()) as T
}

export const api = {
  mapConfig: (signal?: AbortSignal) => getJson<MapConfig>('/api/map/config', signal),
  stops: (signal?: AbortSignal) => getJson<Stop[]>('/api/stops', signal),
  lines: (signal?: AbortSignal) => getJson<Line[]>('/api/lines', signal),
  patternStops: (code: number, signal?: AbortSignal) => getJson<PatternStop[]>(`/api/patterns/${code}/stops`, signal),
  deviceHealth: (signal?: AbortSignal) => getJson<DeviceHealthReport>('/api/quality/devices', signal),
}
