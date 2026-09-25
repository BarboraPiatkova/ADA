import type { HealthStatus } from '../api'

// Small inline SVG icons drawn on a 24px grid with currentColor, so they take the colour
// of the text they sit in. All are decorative (aria-hidden): the word next to them carries
// the meaning.

function Icon({ size = 16, children }: { size?: number; children: React.ReactNode }) {
  return (
    <svg viewBox="0 0 24 24" width={size} height={size} fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      {children}
    </svg>
  )
}

/** The product mark: a route line with stops, as on a line diagram. */
export function BrandMark() {
  return (
    <svg className="text-route" viewBox="0 0 34 20" width="34" height="20" aria-hidden="true">
      <path d="M4 15h9l7-10h10" fill="none" stroke="currentColor" strokeWidth="4" strokeLinecap="round" strokeLinejoin="round" />
      <circle cx="4" cy="15" r="3.2" fill="var(--paper)" stroke="currentColor" strokeWidth="2.2" />
      <circle cx="16.5" cy="10" r="3.2" fill="var(--paper)" stroke="currentColor" strokeWidth="2.2" />
      <circle cx="30" cy="5" r="3.2" fill="var(--paper)" stroke="currentColor" strokeWidth="2.2" />
    </svg>
  )
}

export function StatusIcon({ status, size }: { status: HealthStatus; size?: number }) {
  switch (status) {
    case 'Fault':
      return (
        <Icon size={size}>
          <circle cx="12" cy="12" r="9" />
          <path d="m9 9 6 6M15 9l-6 6" />
        </Icon>
      )
    case 'Warning':
      return (
        <Icon size={size}>
          <path d="M12 3.5 2.5 20h19L12 3.5z" />
          <path d="M12 10v4M12 17h.01" />
        </Icon>
      )
    case 'Ok':
      return (
        <Icon size={size}>
          <circle cx="12" cy="12" r="9" />
          <path d="m8 12.5 2.8 2.8L16 9.5" />
        </Icon>
      )
    default:
      return (
        <Icon size={size}>
          <circle cx="12" cy="12" r="9" />
          <path d="M8 12h8" />
        </Icon>
      )
  }
}

/** Traction pictograms, as printed on timetables. Traction values come from the vehicle log. */
export function TractionIcon({ traction }: { traction: string }) {
  switch (traction) {
    case 'tramvaj':
      return (
        <Icon>
          <path d="M8 2.5h8M12 2.5V6" />
          <rect x="5.5" y="6" width="13" height="12.5" rx="2.5" />
          <path d="M5.5 12.5h13M9 21.5l-1-3M15 21.5l1-3" />
        </Icon>
      )
    case 'trolejbus':
      return (
        <Icon>
          <path d="m9 2 2.5 4M15 2l-2.5 4" />
          <rect x="4" y="6" width="16" height="12" rx="2.5" />
          <path d="M4 12h16M7.5 21v-3M16.5 21v-3" />
        </Icon>
      )
    case 'autobus':
      return (
        <Icon>
          <rect x="4" y="4" width="16" height="14" rx="2.5" />
          <path d="M4 11h16M7.5 21v-3M16.5 21v-3" />
        </Icon>
      )
    default:
      return null
  }
}

export function Chevron() {
  return (
    <Icon size={14}>
      <path d="m6 9 6 6 6-6" />
    </Icon>
  )
}
