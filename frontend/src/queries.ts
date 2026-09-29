import { queryOptions } from '@tanstack/react-query'
import { api, type Period } from './api'

// Every server read goes through TanStack Query: one cache, request de-duplication and
// cancellation (the AbortSignal is passed down to fetch) for free.

export const mapConfigQuery = queryOptions({
  queryKey: ['map', 'config'],
  queryFn: ({ signal }) => api.mapConfig(signal),
  // Deployment configuration — doesn't change while the page is open.
  staleTime: Infinity,
})

export const stopsQuery = queryOptions({
  queryKey: ['network', 'stops'],
  queryFn: ({ signal }) => api.stops(signal),
})

export const linesQuery = queryOptions({
  queryKey: ['network', 'lines'],
  queryFn: ({ signal }) => api.lines(signal),
})

export const patternStopsQuery = (code: number) =>
  queryOptions({
    queryKey: ['network', 'patterns', code, 'stops'],
    queryFn: ({ signal }) => api.patternStops(code, signal),
  })

// The reports only change when new data is imported (the API caches them per import), so
// the page asks again at most every 10 minutes.
const REPORT_STALE_MS = 10 * 60 * 1000

export const deviceHealthQuery = queryOptions({
  queryKey: ['quality', 'devices'],
  queryFn: ({ signal }) => api.deviceHealth(signal),
  staleTime: REPORT_STALE_MS,
})

export const dwellQuery = (line: number | null, period: Period) =>
  queryOptions({
    queryKey: ['operations', 'dwell', line, period],
    queryFn: ({ signal }) => api.dwell(line, period, signal),
    staleTime: REPORT_STALE_MS,
  })

export const stopDwellQuery = (code: number, line: number | null, period: Period) =>
  queryOptions({
    queryKey: ['operations', 'dwell', 'stops', code, line, period],
    queryFn: ({ signal }) => api.stopDwell(code, line, period, signal),
    staleTime: REPORT_STALE_MS,
  })

export const punctualityQuery = (line: number | null, period: Period) =>
  queryOptions({
    queryKey: ['operations', 'punctuality', line, period],
    queryFn: ({ signal }) => api.punctuality(line, period, signal),
    staleTime: REPORT_STALE_MS,
  })

export const loadQuery = (line: number | null, pattern: number | null, period: Period) =>
  queryOptions({
    queryKey: ['operations', 'load', line, pattern, period],
    queryFn: ({ signal }) => api.load(line, pattern, period, signal),
    staleTime: REPORT_STALE_MS,
  })

export const vehicleDayQuery = (vehicle: number, day: string) =>
  queryOptions({
    queryKey: ['operations', 'vehicles', vehicle, day],
    queryFn: ({ signal }) => api.vehicleDay(vehicle, day, signal),
    staleTime: REPORT_STALE_MS,
  })

export const dailyQualityQuery = queryOptions({
  queryKey: ['quality', 'daily'],
  queryFn: ({ signal }) => api.dailyQuality(signal),
  staleTime: REPORT_STALE_MS,
})
