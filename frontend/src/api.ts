// Typed access to the AdaPlatform API. Paths are relative: the Vite dev proxy (and,
// in production, the API serving the SPA) makes them same-origin.

import { accessToken, refresh } from './auth/session'

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
  /** The most frequent destination of trips calling here: which direction this post serves. */
  toward: string | null
  /** Compass direction (0 = north) towards the most frequent next stop. */
  bearing: number | null
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

/** One vehicle on one operating day (the heatmap cell). Shares are null when there were no stops. */
export interface VehicleDay {
  vehicleId: number
  /** ISO date, e.g. "2022-08-01". */
  day: string
  boardings: number
  alightings: number
  imbalance: number | null
  stopSummaries: number
  negativeOccupancyShare: number | null
  flaggedStopShare: number | null
}

export interface DeviceHealthReport {
  from: string | null
  to: string | null
  thresholds: HealthThresholds
  vehicles: VehicleHealth[]
}

/** How long vehicles stand at stops, and how much of it the passengers explain (/api/operations/dwell). */
export interface DwellRules {
  minUnexplainedSeconds: number
  minExcessSeconds: number
  fitMaxSeconds: number
  minVisitsPerStop: number
  onTimeSeconds: number
  minOtherVehiclesAtOnce: number
}

/** dwell ≈ baseSeconds + secondsPerPassenger × (boardings + alightings) */
export interface DwellModel {
  visits: number
  baseSeconds: number
  secondsPerPassenger: number
  correlation: number | null
}

export interface DwellBand {
  minPassengers: number
  /** null: the open top band ("21+"). */
  maxPassengers: number | null
  visits: number
  medianSeconds: number
  p90Seconds: number
}

export interface StopDwell {
  code: number
  name: string
  latitude: number | null
  longitude: number | null
  /** The most frequent destination of trips calling here: which direction this post serves. */
  toward: string | null
  /** Compass direction (0 = north) towards the most frequent next stop. */
  bearing: number | null
  visits: number
  medianSeconds: number
  p90Seconds: number
  meanPassengers: number
  /** Median of dwell minus what its passengers explain; positive = stands longer than they need. */
  medianExcessSeconds: number
  unexplained: number
}

export type DwellCause = 'HeldForTimetable' | 'SeveralVehicles' | 'Other'

export interface UnexplainedDwell {
  /** Local time, ISO without zone. */
  arrival: string
  vehicleId: number
  line: number | null
  stopCode: number
  stopName: string
  dwellSeconds: number
  passengers: number
  expectedSeconds: number
  /** Delay at departure; negative = early. */
  delaySeconds: number
  cause: DwellCause
  /** Other vehicles standing long at the same time. */
  otherVehiclesAtOnce: number
}

/** One visit of one stop, for the stop detail. */
export interface StopVisitDwell {
  arrival: string
  vehicleId: number
  line: number | null
  dwellSeconds: number
  passengers: number
  expectedSeconds: number
  delaySeconds: number
  unexplained: boolean
}

export interface StopDwellDetail {
  code: number
  name: string
  line: number | null
  model: DwellModel
  visits: StopVisitDwell[]
}

export interface VehicleStop {
  sequence: number
  stopCode: number
  stopName: string
  arrival: string | null
  departure: string | null
  /** null: no arrival or no departure in the log (a pass, the terminus). */
  dwellSeconds: number | null
  boardings: number
  alightings: number
  occupancy: number
  delaySeconds: number
  isPassThrough: boolean
}

export interface VehicleTrip {
  id: number
  start: string
  end: string
  line: number | null
  patternCode: number | null
  firstStopName: string | null
  lastStopName: string | null
  isValid: boolean
  isDepotRun: boolean
  stops: VehicleStop[]
}

export interface VehicleTripsDay {
  vehicleId: number
  day: string
  days: string[]
  trips: VehicleTrip[]
}

export interface DwellReport {
  from: string | null
  to: string | null
  line: number | null
  lines: number[]
  timeSource: 'VehicleLog' | 'Transportella'
  rules: DwellRules
  model: DwellModel
  bands: DwellBand[]
  stops: StopDwell[]
  unexplainedTotal: number
  unexplained: UnexplainedDwell[]
}

/** A non-2xx answer from the API. 401: not signed in; 403: signed in, but no permission. */
export class ApiError extends Error {
  readonly path: string
  readonly status: number

  constructor(path: string, status: number) {
    super(`${path} → HTTP ${status}`)
    this.path = path
    this.status = status
  }
}

function send(path: string, signal?: AbortSignal): Promise<Response> {
  const token = accessToken()
  return fetch(path, {
    signal,
    headers: { Accept: 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
  })
}

async function getJson<T>(path: string, signal?: AbortSignal): Promise<T> {
  let response = await send(path, signal)
  // The access token expired (a laptop waking from sleep misses the refresh timer):
  // refresh once and retry. If that fails too, the session is over and the app shows login.
  if (response.status === 401 && (await refresh())) {
    response = await send(path, signal)
  }
  if (!response.ok) {
    throw new ApiError(path, response.status)
  }
  return (await response.json()) as T
}

export const api = {
  mapConfig: (signal?: AbortSignal) => getJson<MapConfig>('/api/map/config', signal),
  stops: (signal?: AbortSignal) => getJson<Stop[]>('/api/stops', signal),
  lines: (signal?: AbortSignal) => getJson<Line[]>('/api/lines', signal),
  patternStops: (code: number, signal?: AbortSignal) => getJson<PatternStop[]>(`/api/patterns/${code}/stops`, signal),
  deviceHealth: (signal?: AbortSignal) => getJson<DeviceHealthReport>('/api/quality/devices', signal),
  dailyQuality: (signal?: AbortSignal) => getJson<VehicleDay[]>('/api/quality/daily', signal),
  dwell: (line: number | null, signal?: AbortSignal) =>
    getJson<DwellReport>(line === null ? '/api/operations/dwell' : `/api/operations/dwell?line=${line}`, signal),
  stopDwell: (code: number, line: number | null, signal?: AbortSignal) =>
    getJson<StopDwellDetail>(`/api/operations/dwell/stops/${code}${line === null ? '' : `?line=${line}`}`, signal),
  vehicleDay: (vehicle: number, day: string, signal?: AbortSignal) =>
    getJson<VehicleTripsDay>(`/api/operations/vehicles/${vehicle}/days/${day}`, signal),
}
