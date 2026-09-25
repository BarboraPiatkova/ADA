// Light / dark / follow-the-system. The choice lives on <html data-theme> (CSS reads it)
// and in localStorage; index.html re-applies it before first paint.

export type ThemeChoice = 'system' | 'light' | 'dark'

const STORAGE_KEY = 'adaplatform.theme'

export function readTheme(): ThemeChoice {
  try {
    const value = localStorage.getItem(STORAGE_KEY)
    return value === 'light' || value === 'dark' ? value : 'system'
  } catch {
    return 'system'
  }
}

export function applyTheme(choice: ThemeChoice) {
  if (choice === 'system') {
    delete document.documentElement.dataset.theme
  } else {
    document.documentElement.dataset.theme = choice
  }
  try {
    if (choice === 'system') localStorage.removeItem(STORAGE_KEY)
    else localStorage.setItem(STORAGE_KEY, choice)
  } catch {
    // Storage blocked — the choice holds for this page view only.
  }
}
