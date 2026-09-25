// Per-browser preferences (theme, base map). Storage can be blocked (private mode, site
// data off) and then throws; a preference is then simply not remembered.

export function readSetting(key: string): string | null {
  try {
    return localStorage.getItem(key)
  } catch {
    return null
  }
}

/** Stores `value`, or forgets the setting when it's null. */
export function writeSetting(key: string, value: string | null) {
  try {
    if (value === null) localStorage.removeItem(key)
    else localStorage.setItem(key, value)
  } catch {
    // Not remembered; the choice holds for this page view only.
  }
}
