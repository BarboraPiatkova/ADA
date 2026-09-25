import type { ComponentProps } from 'react'
import { cn } from './cn'

/** A text-styled button for secondary actions ("clear filters", "try again"). */
export function LinkButton({ className, ...props }: ComponentProps<'button'>) {
  return <button className={cn('cursor-pointer p-0 font-semibold text-route hover:text-route-strong hover:underline', className)} {...props} />
}
