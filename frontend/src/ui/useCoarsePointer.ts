import { useSyncExternalStore } from 'react'

const query = typeof window === 'undefined' ? null : window.matchMedia('(pointer: coarse)')

/** True on touch-first devices (finger, not mouse): targets there need 44px, not 24px. */
export function useCoarsePointer(): boolean {
  return useSyncExternalStore(
    (onChange) => {
      query?.addEventListener('change', onChange)
      return () => query?.removeEventListener('change', onChange)
    },
    () => query?.matches ?? false,
  )
}
