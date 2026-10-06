import { openScreen } from '../navigation'
import type { ScreenId } from '../screens'
import { LinkButton } from '../ui/LinkButton'

/** A quiet link to the same thing on another screen, e.g. from a stop's punctuality to its dwell. */
export function ScreenLink({ screen, params, label, title }: { screen: ScreenId; params: Record<string, number | null>; label: string; title: string }) {
  return (
    <LinkButton className="ml-2 text-xs font-normal whitespace-nowrap" title={title} aria-label={title} onClick={() => openScreen(screen, params)}>
      {label} ›
    </LinkButton>
  )
}
