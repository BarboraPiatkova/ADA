import { ToggleGroup } from 'radix-ui'
import type { ComponentProps } from 'react'
import { cn } from './cn'

/**
 * Segmented control (Radix ToggleGroup): theme, language, chart/table, grouping.
 * The selected option is unmistakable — filled in the product colour — rather than a
 * subtle white-on-grey lift that is hard to see on a white card.
 */
export function SegmentedRoot({ className, ...props }: ComponentProps<typeof ToggleGroup.Root>) {
  return <ToggleGroup.Root className={cn('inline-flex gap-0.5 rounded-lg border border-rule bg-paper p-0.5', className)} {...props} />
}

export function SegmentedItem({ className, ...props }: ComponentProps<typeof ToggleGroup.Item>) {
  return (
    <ToggleGroup.Item
      className={cn(
        'inline-flex h-[26px] min-w-[30px] touch-target cursor-pointer items-center justify-center rounded-md px-[9px] font-display text-sm font-semibold text-ink-2',
        'hover:bg-surface hover:text-ink focus-visible:outline-offset-1',
        'data-[state=on]:cursor-default data-[state=on]:bg-route data-[state=on]:text-on-route',
        className,
      )}
      {...props}
    />
  )
}
