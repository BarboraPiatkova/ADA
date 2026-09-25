// AdaPlatform's permissions, as the API names them (src/AdaPlatform.Api/Auth/Permissions.cs,
// keep in step). Each is granted through a role of the AdaPlatform application in Tokari.

export const PERMISSIONS = {
  /** Network map: stops, lines, patterns. */
  networkRead: 'network:read',
  /** Device health and data quality. */
  qualityRead: 'quality:read',
} as const

export type Permission = (typeof PERMISSIONS)[keyof typeof PERMISSIONS]
