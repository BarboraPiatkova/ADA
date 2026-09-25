import { Select as RadixSelect } from 'radix-ui'

export interface SelectOption {
  value: string
  label: string
}

/** A styled Radix Select. Radix doesn't allow an empty-string value, so "all" is a real option. */
export function Select({
  label,
  value,
  options,
  onChange,
}: {
  label: string
  value: string
  options: SelectOption[]
  onChange: (value: string) => void
}) {
  return (
    <RadixSelect.Root value={value} onValueChange={onChange}>
      <RadixSelect.Trigger
        className="inline-flex h-8 cursor-pointer items-center gap-1.5 pointer-coarse:h-11 rounded-lg border border-rule bg-paper px-2.5 hover:border-ink-2"
        aria-label={label}
      >
        <span className="text-ink-2">{label}:</span> <RadixSelect.Value />
        <RadixSelect.Icon className="text-ink-2">▾</RadixSelect.Icon>
      </RadixSelect.Trigger>
      <RadixSelect.Portal>
        <RadixSelect.Content
          className="z-[1000] min-w-(--radix-select-trigger-width) rounded-lg border border-rule bg-paper p-1 shadow-float"
          position="popper"
          sideOffset={4}
        >
          <RadixSelect.Viewport>
            {options.map((o) => (
              <RadixSelect.Item
                key={o.value}
                value={o.value}
                className="flex cursor-pointer justify-between gap-3 rounded-md px-2.5 py-1.5 outline-none pointer-coarse:py-3 data-highlighted:bg-route-soft"
              >
                <RadixSelect.ItemText>{o.label}</RadixSelect.ItemText>
                <RadixSelect.ItemIndicator className="text-route">✓</RadixSelect.ItemIndicator>
              </RadixSelect.Item>
            ))}
          </RadixSelect.Viewport>
        </RadixSelect.Content>
      </RadixSelect.Portal>
    </RadixSelect.Root>
  )
}
