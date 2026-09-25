import type { HealthReason } from '../api'

const pct = new Intl.NumberFormat('cs-CZ', { style: 'percent', maximumFractionDigits: 0 })
const share = (r: HealthReason) => pct.format(r.value ?? 0)

/** Phrases a reason code from the API. */
export function formatReason(r: HealthReason): string {
  switch (r.code) {
    case 'DeviceSilent':
      return `za ${r.value} zastavení nenapočítala nikoho`
    case 'DeviceFlagged':
      return `označena jako chybná na ${share(r)} zastavení`
    case 'DeviceNotAlive':
      return `${share(r)} zpráv o stavu hlásí alive=false`
    case 'AllDevicesSilent':
      return 'žádná jednotka nenapočítala nikoho'
    case 'SomeDevicesSilent':
      return `${r.value} jednotek nenapočítalo nikoho`
    case 'Imbalance':
      return `nesoulad nástupů a výstupů ${share(r)}`
    case 'NegativeOccupancy':
      return `záporná obsazenost na ${share(r)} zastavení`
    case 'FlaggedStops':
      return `příznak chyby na ${share(r)} zastavení`
    case 'DeviceWarning':
      return 'varování u některé jednotky'
  }
}

export const LAYER_NAMES: Record<string, string> = {
  'mapy-basic': 'Mapy.com – základní',
  'mapy-outdoor': 'Mapy.com – turistická',
  'mapy-aerial': 'Mapy.com – letecká',
  'mapy-winter': 'Mapy.com – zimní',
  osm: 'OpenStreetMap',
}
