import { lazy, type ComponentType, type LazyExoticComponent, type ReactNode } from 'react'
import { PERMISSIONS, type Permission } from './auth/permissions'
import { MapSkeleton } from './map/MapSkeleton'
import { HealthSkeleton } from './quality/HealthSkeleton'

/**
 * The app's screens: the tab bar, the tab content and who may see what all come from this
 * list. Each screen's code (the map pulls in Leaflet) loads only when it's first opened;
 * until then its skeleton shows, which lives outside the lazy chunk.
 */
export interface Screen {
  /** URL hash and i18n key (app.views.<id>). */
  id: 'mapa' | 'jednotky' | 'provoz' | 'dochvilnost' | 'obsazenost' | 'vozidla'
  permission: Permission
  Component: LazyExoticComponent<ComponentType>
  skeleton: (label: string) => ReactNode
}

export const SCREENS: readonly Screen[] = [
  {
    id: 'mapa',
    permission: PERMISSIONS.networkRead,
    Component: lazy(() => import('./map/NetworkMapScreen')),
    skeleton: (label) => <MapSkeleton label={label} />,
  },
  {
    id: 'jednotky',
    permission: PERMISSIONS.qualityRead,
    Component: lazy(() => import('./quality/DeviceHealthView').then((m) => ({ default: m.DeviceHealthView }))),
    skeleton: (label) => <HealthSkeleton label={label} />,
  },
  {
    id: 'provoz',
    permission: PERMISSIONS.operationsRead,
    Component: lazy(() => import('./operations/DwellScreen').then((m) => ({ default: m.DwellScreen }))),
    skeleton: (label) => <HealthSkeleton label={label} />,
  },
  {
    id: 'dochvilnost',
    permission: PERMISSIONS.operationsRead,
    Component: lazy(() => import('./operations/PunctualityScreen').then((m) => ({ default: m.PunctualityScreen }))),
    skeleton: (label) => <HealthSkeleton label={label} />,
  },
  {
    id: 'obsazenost',
    permission: PERMISSIONS.operationsRead,
    Component: lazy(() => import('./operations/LoadScreen').then((m) => ({ default: m.LoadScreen }))),
    skeleton: (label) => <HealthSkeleton label={label} />,
  },
  {
    id: 'vozidla',
    permission: PERMISSIONS.operationsRead,
    Component: lazy(() => import('./fleet/VehiclesScreen').then((m) => ({ default: m.VehiclesScreen }))),
    skeleton: (label) => <HealthSkeleton label={label} />,
  },
]

export type ScreenId = Screen['id']
