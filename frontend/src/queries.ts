import { queryOptions } from '@tanstack/react-query'
import { api } from './api'

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

export const deviceHealthQuery = queryOptions({
  queryKey: ['quality', 'devices'],
  queryFn: ({ signal }) => api.deviceHealth(signal),
  // The API caches this report for 10 minutes; asking sooner returns the same data.
  staleTime: 10 * 60 * 1000,
})
