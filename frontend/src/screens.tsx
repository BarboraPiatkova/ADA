import { lazy, type ComponentType, type LazyExoticComponent, type ReactNode } from 'react'
import { PERMISSIONS, type Permission } from './auth/permissions'
import { MapSkeleton } from './map/MapSkeleton'
import { HealthSkeleton } from './quality/HealthSkeleton'

/**
 * The tab bar's groups, in order, laid out like ADA's menus: the map, the statistics, the data (vehicles,
 * trips, stops) and the counting units. A group with one screen is that screen; a group with more shows
 * them as sub-tabs. i18n key app.groups.<id>.
 */
export const GROUPS = ['mapa', 'statistiky', 'data', 'jednotky'] as const
export type GroupId = (typeof GROUPS)[number]

/**
 * The app's screens: the tab bar, the tab content and who may see what all come from this
 * list. Each screen's code (the map pulls in Leaflet) loads only when it's first opened;
 * until then its skeleton shows, which lives outside the lazy chunk.
 */
export interface Screen {
  /** URL hash and i18n key (app.views.<id>). */
  id: 'mapa' | 'jednotky' | 'provoz' | 'dochvilnost' | 'obsazenost' | 'vozidla' | 'jizdy' | 'zastavky'
  group: GroupId
  permission: Permission
  Component: LazyExoticComponent<ComponentType>
  skeleton: (label: string) => ReactNode
}

export const SCREENS: readonly Screen[] = [
  {
    id: 'mapa',
    group: 'mapa',
    permission: PERMISSIONS.networkRead,
    Component: lazy(() => import('./map/NetworkMapScreen')),
    skeleton: (label) => <MapSkeleton label={label} />,
  },
  {
    id: 'jednotky',
    group: 'jednotky',
    permission: PERMISSIONS.qualityRead,
    Component: lazy(() => import('./quality/DeviceHealthView').then((m) => ({ default: m.DeviceHealthView }))),
    skeleton: (label) => <HealthSkeleton label={label} />,
  },
  {
    id: 'provoz',
    group: 'statistiky',
    permission: PERMISSIONS.operationsRead,
    Component: lazy(() => import('./operations/DwellScreen').then((m) => ({ default: m.DwellScreen }))),
    skeleton: (label) => <HealthSkeleton label={label} />,
  },
  {
    id: 'dochvilnost',
    group: 'statistiky',
    permission: PERMISSIONS.operationsRead,
    Component: lazy(() => import('./operations/PunctualityScreen').then((m) => ({ default: m.PunctualityScreen }))),
    skeleton: (label) => <HealthSkeleton label={label} />,
  },
  {
    id: 'obsazenost',
    group: 'statistiky',
    permission: PERMISSIONS.operationsRead,
    Component: lazy(() => import('./operations/LoadScreen').then((m) => ({ default: m.LoadScreen }))),
    skeleton: (label) => <HealthSkeleton label={label} />,
  },
  {
    id: 'vozidla',
    group: 'data',
    permission: PERMISSIONS.operationsRead,
    Component: lazy(() => import('./fleet/VehiclesScreen').then((m) => ({ default: m.VehiclesScreen }))),
    skeleton: (label) => <HealthSkeleton label={label} />,
  },
  {
    id: 'jizdy',
    group: 'data',
    permission: PERMISSIONS.operationsRead,
    Component: lazy(() => import('./trips/TripsScreen').then((m) => ({ default: m.TripsScreen }))),
    skeleton: (label) => <HealthSkeleton label={label} />,
  },
  {
    id: 'zastavky',
    group: 'data',
    permission: PERMISSIONS.operationsRead,
    Component: lazy(() => import('./stops/StopsScreen').then((m) => ({ default: m.StopsScreen }))),
    skeleton: (label) => <HealthSkeleton label={label} />,
  },
]

export type ScreenId = Screen['id']

/** A screen's place in the URL hash: "mapa" for a group of one, "statistiky/provoz" inside a group. */
export function screenPath(id: ScreenId) {
  const group = SCREENS.find((s) => s.id === id)?.group
  return group === undefined || group === id ? id : `${group}/${id}`
}
