import { ToggleGroup } from 'radix-ui'
import { useState } from 'react'
import { applyTheme, readTheme, type ThemeChoice } from '../theme'

const ICONS: Record<ThemeChoice, string> = {
  system: 'M4 5h16v11H4zM9 20h6M12 16v4',
  light: 'M12 4v2M12 18v2M4 12h2M18 12h2M6.3 6.3l1.4 1.4M16.3 16.3l1.4 1.4M6.3 17.7l1.4-1.4M16.3 7.7l1.4-1.4M12 8a4 4 0 1 0 0 8a4 4 0 1 0 0-8',
  dark: 'M20 14.5A8 8 0 0 1 9.5 4a8 8 0 1 0 10.5 10.5z',
}

const LABELS: Record<ThemeChoice, string> = {
  system: 'Podle systému',
  light: 'Světlý režim',
  dark: 'Tmavý režim',
}

export function ThemeSwitch() {
  const [choice, setChoice] = useState<ThemeChoice>(readTheme)

  return (
    <ToggleGroup.Root
      type="single"
      className="segmented"
      aria-label="Barevný režim"
      value={choice}
      onValueChange={(value) => {
        if (!value) return // clicking the active item would otherwise clear the choice
        const next = value as ThemeChoice
        setChoice(next)
        applyTheme(next)
      }}
    >
      {(Object.keys(ICONS) as ThemeChoice[]).map((c) => (
        <ToggleGroup.Item key={c} value={c} className="segmented-item" aria-label={LABELS[c]} title={LABELS[c]}>
          <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
            <path d={ICONS[c]} />
          </svg>
        </ToggleGroup.Item>
      ))}
    </ToggleGroup.Root>
  )
}
