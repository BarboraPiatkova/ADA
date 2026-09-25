import { Skeleton, SkeletonScreen, TableSkeleton } from '../ui/Skeleton'

// Column widths of the vehicle table: status pill, vehicle, traction, the four numbers,
// flagged share, reason. Only the proportions matter.
const TABLE_COLUMNS = ['w-20', 'w-12', 'w-40', 'w-14', 'w-14', 'w-12', 'w-14', 'w-12', 'w-56']

/** The device-health screen's layout while its report is computed (several seconds). */
export function HealthSkeleton({ label }: { label: string }) {
  return (
    <SkeletonScreen label={label} className="flex-1 overflow-hidden p-4 md:px-7 md:pt-6 md:pb-10">
      <Skeleton className="mb-3 h-7 w-72 max-w-full" />
      <div className="mb-2 flex flex-wrap gap-7">
        <Skeleton className="h-4 w-44" />
        <Skeleton className="h-4 w-24" />
        <Skeleton className="h-4 w-24" />
      </div>
      <Skeleton className="h-3.5 w-[min(560px,100%)]" />

      <Skeleton className="mt-[22px] mb-2.5 h-3.5 w-full" />
      <div className="mb-[18px] flex flex-wrap gap-2">
        {['w-28', 'w-28', 'w-28', 'w-28'].map((w, i) => (
          <Skeleton key={i} className={`h-9 ${w} rounded-full`} />
        ))}
      </div>

      <div className="mb-3 flex flex-wrap gap-4">
        <Skeleton className="h-8 w-[220px] rounded-lg" />
        <Skeleton className="h-8 w-36 rounded-lg" />
        <Skeleton className="h-8 w-48 rounded-lg" />
      </div>

      <ChartCardSkeleton plot="h-[180px]" />
      <div className="mt-4 mb-7 grid grid-cols-1 gap-4 md:grid-cols-[repeat(auto-fit,minmax(420px,1fr))]">
        <ChartCardSkeleton plot="h-[200px]" />
        <ChartCardSkeleton plot="h-[200px]" />
      </div>

      <Skeleton className="mb-3 h-6 w-32" />
      <TableSkeleton columns={TABLE_COLUMNS} rows={8} />
    </SkeletonScreen>
  )
}

/** One chart card: title, subtitle, plot area — the same frame as ChartFigure. */
function ChartCardSkeleton({ plot }: { plot: string }) {
  return (
    <div className="rounded-[10px] border border-rule bg-paper px-[18px] pt-4 pb-3.5">
      <Skeleton className="mb-2 h-5 w-64 max-w-full" />
      <Skeleton className="mb-4 h-3.5 w-96 max-w-full" />
      <Skeleton className={`w-full ${plot}`} />
    </div>
  )
}
