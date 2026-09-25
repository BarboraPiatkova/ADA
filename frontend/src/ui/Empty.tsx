import type { ReactNode } from 'react'
import { cn } from './cn'

/** Centred message for loading, empty and error states. */
export function Empty({ children, error = false }: { children: ReactNode; error?: boolean }) {
  return <p className={cn('m-auto max-w-[60ch] p-6 text-center', error ? 'text-fault' : 'text-ink-2')}>{children}</p>
}
