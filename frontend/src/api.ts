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

/** One timetable version of a route: the hours its trips start in (null without trips). */
export interface PatternVariant {
  code: number
  trips: number
  firstHour: number | null
  lastHour: number | null
}

/** A route of a line: its patterns with the same stops (timetable variants) as one entry. */
export interface PatternSummary {
  /** The busiest variant's code: draws the route. */
  code: number
  firstStopName: string | null
  lastStopName: string | null
  stopCount: number
  /** Trips of every variant. */
  trips: number
  variants: PatternVariant[]
  /** Stops it calls at that the busiest route between the same termini doesn't. */
  extraStops: string[]
  /** Stops the busiest route between the same termini calls at that it leaves out. */
  missingStops: string[]
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
  /** Every service day with a vehicle log, whatever period the report covers: the period picker's days. */
  days: string[]
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
  /** Every day with data, whatever period the report covers: the days the period picker offers. */
  days: string[]
}

export interface PunctualityRules {
  earlySeconds: number
  lateSeconds: number
  veryLateSeconds: number
  minDeparturesPerStop: number
}

export interface PunctualitySummary {
  departures: number
  early: number
  onTime: number
  late: number
  veryLate: number
  /** Negative = early. */
  medianDelaySeconds: number
  /** Passengers on board × minutes beyond the on-time limit, summed. */
  passengerMinutesLate: number
  /** Share of passengers on board who left on time; null without trusted counts. */
  passengersOnTimeShare: number | null
}

export interface PunctualityStop {
  code: number
  name: string
  latitude: number | null
  longitude: number | null
  toward: string | null
  bearing: number | null
  summary: PunctualitySummary
}

/** Where arrival and departure times come from: the vehicles' logs (with passengers) or Transportella (times only). */
export type TimesSource = 'vehicleLog' | 'transportella'

export interface PunctualityReport {
  /** False for Transportella's times: nobody was counted, so the passenger figures are empty. */
  hasPassengers: boolean
  from: string | null
  to: string | null
  line: number | null
  lines: number[]
  rules: PunctualityRules
  total: PunctualitySummary
  hours: { hour: number; summary: PunctualitySummary }[]
  /** Weekday 1 = Monday … 7 = Sunday; days = how many such days the period has. */
  weekdays: { weekday: number; days: number; summary: PunctualitySummary }[]
  weekHours: { weekday: number; hour: number; summary: PunctualitySummary }[]
  byLine: { line: number; summary: PunctualitySummary }[]
  stops: PunctualityStop[]
  days: string[]
}

/** A route on the occupancy picker (its timetable variants as one). */
export interface LoadPattern {
  code: number
  line: number | null
  firstStopName: string | null
  lastStopName: string | null
  trips: number
  extraStops: string[]
  missingStops: string[]
}

export interface LoadProfileStop {
  sequence: number
  stopCode: number
  stopName: string
  trips: number
  meanBoardings: number
  meanAlightings: number
  medianLoad: number
  p90Load: number
  maxLoad: number
}

export interface CrowdedTrip {
  tripId: number
  vehicleId: number
  start: string
  line: number | null
  patternCode: number | null
  firstStopName: string | null
  lastStopName: string | null
  peakLoad: number
  peakStopName: string | null
  peakStopCode: number | null
  boardings: number
  capacity: number | null
  /** Peak load as a share of capacity; null when the capacity is unknown. */
  peakShare: number | null
}

export interface LoadReport {
  from: string | null
  to: string | null
  line: number | null
  lines: number[]
  trips: number
  boardings: number
  tripsWithCapacity: number
  boardingsByHour: { hour: number; boardings: number; alightings: number }[]
  patterns: LoadPattern[]
  pattern: number | null
  profile: LoadProfileStop[]
  /** The profile shows the best covered pattern because the reader picked none. */
  patternChosenForReader: boolean
  crowded: CrowdedTrip[]
  /** Weekday 1 = Monday … 7 = Sunday; days = how many such days the period has (boardings are totals). */
  boardingsByWeekday: { weekday: number; days: number; boardings: number; alightings: number }[]
  boardingsByWeekHour: { weekday: number; hour: number; days: number; boardings: number }[]
  days: string[]
}

/** One vehicle of the fleet register, with what its logs hold for the period (invalid trips and depot runs included). */
export interface FleetVehicle {
  id: number
  depot: string | null
  traction: string | null
  model: string | null
  seatingCapacity: number | null
  standingCapacity: number | null
  /** Seats plus standing places; null unless both are known. */
  capacity: number | null
  isExcluded: boolean
  devices: number
  /** Last service day a log file of the vehicle was imported for (ADA: "Poslední data"). */
  lastData: string | null
  days: number
  trips: number
  invalidTrips: number
  boardings: number
  alightings: number
  /** Known device faults that began in the period. */
  faults: number
}

export interface FleetReport {
  from: string | null
  to: string | null
  vehicles: FleetVehicle[]
  /** Every day with a trip from the vehicle logs, for the period picker. */
  days: string[]
}

export interface VehicleDevice {
  deviceNumber: number
  firmwareVersion: string | null
  firstSeen: string
  lastSeen: string
}

export interface VehicleDaySummary {
  day: string
  trips: number
  invalidTrips: number
  depotRuns: number
  boardings: number
  alightings: number
  firstStart: string
  lastEnd: string
}

export type FaultKind = 'UnexpectedRestart' | 'PowerCutUntilEnd' | 'PowerCutStatusFalse' | 'InvalidPassengerCount'

export interface VehicleFault {
  id: number
  deviceNumber: number | null
  tripId: number | null
  from: string
  to: string | null
  kind: FaultKind
  source: 'LegacyAda' | 'Detector'
  details: string | null
}

export interface VehicleDetail {
  vehicle: FleetVehicle
  devices: VehicleDevice[]
  tripDays: VehicleDaySummary[]
  faults: VehicleFault[]
  days: string[]
}

/** A trip in the trip list: invalid ones and depot runs included, marked. */
export interface TripRow {
  id: number
  vehicleId: number
  /** The second unit of a coupled set, if any. */
  secondVehicleId: number | null
  start: string
  end: string
  line: number | null
  patternCode: number | null
  /** Service block (ADA: služba). */
  block: string | null
  firstStopName: string | null
  lastStopName: string | null
  /** Stops served (pass-throughs not counted). */
  stops: number
  boardings: number
  alightings: number
  isValid: boolean
  isDepotRun: boolean
  /** Stops where a counting unit flagged its count as invalid. */
  flaggedStops: number
}

export interface TripList {
  from: string | null
  to: string | null
  line: number | null
  vehicle: number | null
  lines: number[]
  vehicles: number[]
  /** Trips matching the filter; the list holds at most 20 000 of them. */
  total: number
  trips: TripRow[]
  days: string[]
}

/** ADA's "Přehled" of a trip. */
export interface TripOverview {
  km: number | null
  /** Kilometres times the units in the set. */
  vehicleKm: number | null
  /** Kilometres times the vehicle's capacity. */
  placeKm: number | null
  /** Everyone who boarded. */
  passengers: number
}

export interface FlaggedStop {
  sequence: number
  stopCode: number
  deviceNumbers: number[]
}

export interface TripDetail {
  trip: VehicleTrip
  vehicleId: number
  secondVehicleId: number | null
  block: string | null
  capacity: number | null
  overview: TripOverview
  flaggedStops: FlaggedStop[]
  faults: VehicleFault[]
}

/** One stop post over the filtered trips. */
export interface StopStat {
  code: number
  name: string
  /** The direction the post serves. */
  toward: string | null
  /** Calls where the vehicle stopped. */
  visits: number
  passThroughs: number
  boardings: number
  alightings: number
  /** Mean passengers on board after the stop. */
  meanLoad: number
  maxLoad: number
  lines: number[]
}

/** Which trips the stop figures count: those the statistics use (valid, no depot runs), or all. */
export type StopTrips = 'valid' | 'all'

export interface StopStatistics {
  from: string | null
  to: string | null
  line: number | null
  vehicle: number | null
  allTrips: boolean
  lines: number[]
  vehicles: number[]
  trips: number
  stops: StopStat[]
  days: string[]
}

export interface StopStatisticsDetail {
  code: number
  name: string
  toward: string | null
  byLine: { line: number | null; visits: number; boardings: number; alightings: number; meanLoad: number }[]
  byHour: { hour: number; visits: number; boardings: number; alightings: number }[]
  /** weekday: 1 = Monday … 7 = Sunday; days: how many such days had calls here. */
  byWeekday: { weekday: number; days: number; boardings: number; alightings: number }[]
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

/**
 * Which days a report counts, as timetables run: working days (all, in school term, in school holidays),
 * Saturdays, Sundays with public holidays, or public holidays alone.
 */
export type DayKind = 'all' | 'workdays' | 'schoolWorkdays' | 'holidayWorkdays' | 'saturday' | 'sundayOrHoliday' | 'publicHoliday'

/** A report's days: from–to, both included (either end may be open), and which days of the week. */
export type Period = { from?: string; to?: string; days?: Exclude<DayKind, 'all'> }

/** "?line=1&from=…&to=…" with only the parameters that are set, or "". */
function operationsQuery(params: { line?: number | null; pattern?: number | null; vehicle?: number | null; trips?: string; from?: string; to?: string; days?: string; times?: string }) {
  const query = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) if (value !== null && value !== undefined) query.set(key, String(value))
  const qs = query.toString()
  return qs ? `?${qs}` : ''
}

export const api = {
  mapConfig: (signal?: AbortSignal) => getJson<MapConfig>('/api/map/config', signal),
  stops: (signal?: AbortSignal) => getJson<Stop[]>('/api/stops', signal),
  lines: (signal?: AbortSignal) => getJson<Line[]>('/api/lines', signal),
  patternStops: (code: number, signal?: AbortSignal) => getJson<PatternStop[]>(`/api/patterns/${code}/stops`, signal),
  deviceHealth: (period: Period, signal?: AbortSignal) => getJson<DeviceHealthReport>(`/api/quality/devices${operationsQuery({ ...period })}`, signal),
  dailyQuality: (period: Period, signal?: AbortSignal) => getJson<VehicleDay[]>(`/api/quality/daily${operationsQuery({ ...period })}`, signal),
  dwell: (line: number | null, period: Period, signal?: AbortSignal) =>
    getJson<DwellReport>(`/api/operations/dwell${operationsQuery({ line, ...period })}`, signal),
  stopDwell: (code: number, line: number | null, period: Period, signal?: AbortSignal) =>
    getJson<StopDwellDetail>(`/api/operations/dwell/stops/${code}${operationsQuery({ line, ...period })}`, signal),
  punctuality: (line: number | null, period: Period, times: TimesSource, signal?: AbortSignal) =>
    getJson<PunctualityReport>(`/api/operations/punctuality${operationsQuery({ line, ...period, times: times === 'transportella' ? times : undefined })}`, signal),
  load: (line: number | null, pattern: number | null, period: Period, signal?: AbortSignal) =>
    getJson<LoadReport>(`/api/operations/load${operationsQuery({ line, pattern, ...period })}`, signal),
  vehicleDay: (vehicle: number, day: string, signal?: AbortSignal) =>
    getJson<VehicleTripsDay>(`/api/operations/vehicles/${vehicle}/days/${day}`, signal),
  fleet: (period: Period, signal?: AbortSignal) => getJson<FleetReport>(`/api/fleet/vehicles${operationsQuery({ ...period })}`, signal),
  vehicle: (vehicle: number, period: Period, signal?: AbortSignal) =>
    getJson<VehicleDetail>(`/api/fleet/vehicles/${vehicle}${operationsQuery({ ...period })}`, signal),
  trips: (line: number | null, vehicle: number | null, period: Period, signal?: AbortSignal) =>
    getJson<TripList>(`/api/trips${operationsQuery({ line, vehicle, ...period })}`, signal),
  trip: (id: number, signal?: AbortSignal) => getJson<TripDetail>(`/api/trips/${id}`, signal),
  stopStatistics: (line: number | null, vehicle: number | null, trips: StopTrips, period: Period, signal?: AbortSignal) =>
    getJson<StopStatistics>(`/api/stop-statistics${operationsQuery({ line, vehicle, trips: trips === 'all' ? trips : undefined, ...period })}`, signal),
  stopStatisticsDetail: (code: number, line: number | null, vehicle: number | null, trips: StopTrips, period: Period, signal?: AbortSignal) =>
    getJson<StopStatisticsDetail>(`/api/stop-statistics/${code}${operationsQuery({ line, vehicle, trips: trips === 'all' ? trips : undefined, ...period })}`, signal),
}
