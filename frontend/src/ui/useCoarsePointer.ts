import { useSyncExternalStore } from 'react'

const query = typeof window === 'undefined' ? null : window.matchMedia('(pointer: coarse)')

// Module-level, so the listener isn't removed and re-added on every render.
function subscribe(onChange: () => void) {
  query?.addEventListener('change', onChange)
  return () => query?.removeEventListener('change', onChange)
}

const isCoarse = () => query?.matches ?? false

/** True on touch-first devices (finger, not mouse): targets there need 44px, not 24px. */
export function useCoarsePointer(): boolean {
  return useSyncExternalStore(subscribe, isCoarse)
}
