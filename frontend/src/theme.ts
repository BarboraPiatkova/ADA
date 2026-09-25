// Light / dark / follow-the-system. The choice lives in localStorage; <html data-theme>
// always holds the resolved theme (light or dark), which is what the CSS reads.
// public/theme-init.js does the same resolution before first paint.

import { readSetting, writeSetting } from './ui/storage'

export type ThemeChoice = 'system' | 'light' | 'dark'

const STORAGE_KEY = 'adaplatform.theme'

export function readTheme(): ThemeChoice {
  const value = readSetting(STORAGE_KEY)
  return value === 'light' || value === 'dark' ? value : 'system'
}

const systemDark = window.matchMedia('(prefers-color-scheme: dark)')

/** Sets <html data-theme> to what's shown now: the choice, or the system's when following it. */
function resolve(choice: ThemeChoice) {
  document.documentElement.dataset.theme = choice === 'system' ? (systemDark.matches ? 'dark' : 'light') : choice
}

// Following the system: switch along when it switches (e.g. at sunset).
systemDark.addEventListener('change', () => {
  if (readTheme() === 'system') resolve('system')
})

export function applyTheme(choice: ThemeChoice) {
  resolve(choice)
  writeSetting(STORAGE_KEY, choice === 'system' ? null : choice)
}
