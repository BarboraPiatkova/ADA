import type { TFunction } from 'i18next'
import type { VehicleHealth } from '../api'

/** Traction comes from the vehicle's own log (in Czech); show it in the UI language. */
export function tractionLabel(t: TFunction, traction: string) {
  return t(`health.tractions.${traction}` as 'health.tractions.tramvaj', { defaultValue: traction })
}

/**
 * What search matches: a vehicle's number, model, traction (as stored and as shown) and the
 * firmware of its devices — whatever a dispatcher is likely to type.
 */
export function searchText(t: TFunction, v: VehicleHealth) {
  return [v.vehicleId, v.model ?? '', v.traction ?? '', v.traction ? tractionLabel(t, v.traction) : '', ...v.devices.map((d) => d.firmwareVersion ?? '')]
    .join(' ')
    .toLowerCase()
}
