import { Skeleton, SkeletonScreen } from '../ui/Skeleton'

const LINE_WIDTHS = ['w-40', 'w-32', 'w-44', 'w-36', 'w-28', 'w-40', 'w-32', 'w-36', 'w-44', 'w-28']

/** The line list's frame with placeholder rows (same width and padding as LinePicker). */
export function LinePickerSkeleton({ label }: { label: string }) {
  return (
    <SkeletonScreen
      label={label}
      className="max-h-[35svh] shrink-0 overflow-hidden border-b border-rule bg-paper px-3.5 py-[18px] md:max-h-none md:w-[300px] md:border-r md:border-b-0"
    >
      <Skeleton className="mx-1.5 mb-2 h-6 w-20" />
      <Skeleton className="mx-1.5 mb-4 h-3.5 w-56" />
      {LINE_WIDTHS.map((w, i) => (
        <div key={i} className="flex items-center gap-3 px-1.5 py-[9px]">
          <Skeleton className="h-6 w-10 rounded-md" />
          <Skeleton className={`h-4 ${w}`} />
        </div>
      ))}
    </SkeletonScreen>
  )
}

/** Whole map screen: the line list and a map area that is clearly not a map yet. */
export function MapSkeleton({ label }: { label: string }) {
  return (
    <div className="flex min-w-0 flex-1 flex-col md:flex-row">
      <LinePickerSkeleton label={label} />
      <div className="relative flex min-h-[300px] flex-1 bg-surface" aria-hidden="true">
        <Skeleton className="absolute top-3 right-3 h-[92px] w-28 rounded-[10px]" />
      </div>
    </div>
  )
}
