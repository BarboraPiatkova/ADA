import type { TFunction } from 'i18next'

/** Up to three stop names, then how many more ("Řečkovice, Filkukova, Tylova +12"). */
function someStops(names: string[]) {
  return names.length > 3 ? `${names.slice(0, 3).join(', ')} +${names.length - 3}` : names.join(', ')
}

/** What sets a route apart from the busiest one between the same termini: stops it adds or leaves out. */
export function routeDifference(t: TFunction, p: { extraStops: string[]; missingStops: string[] }) {
  return [
    p.extraStops.length > 0 ? t('map.extraStops', { stops: someStops(p.extraStops) }) : null,
    p.missingStops.length > 0 ? t('map.missingStops', { stops: someStops(p.missingStops) }) : null,
  ]
    .filter(Boolean)
    .join('; ')
}
