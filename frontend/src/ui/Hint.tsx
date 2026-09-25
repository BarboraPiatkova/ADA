import { Tooltip } from 'radix-ui'
import type { ReactNode } from 'react'

/** Radix tooltip with the app's styling — for explaining column headers and terms. */
export function Hint({ text, children }: { text: string; children: ReactNode }) {
  return (
    <Tooltip.Root>
      <Tooltip.Trigger asChild>{children}</Tooltip.Trigger>
      <Tooltip.Portal>
        <Tooltip.Content className="z-[1000] max-w-[280px] rounded-[7px] bg-ink px-2.5 py-[7px] text-xs leading-snug text-paper" sideOffset={6}>
          {text}
          <Tooltip.Arrow className="fill-ink" />
        </Tooltip.Content>
      </Tooltip.Portal>
    </Tooltip.Root>
  )
}
