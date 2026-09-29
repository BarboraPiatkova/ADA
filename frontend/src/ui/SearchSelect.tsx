import { Popover } from 'radix-ui'
import { useEffect, useId, useState } from 'react'
import { cn } from './cn'
import type { SelectOption } from './Select'

const fold = (text: string) => text.normalize('NFD').replace(/\p{Diacritic}/gu, '').toLowerCase()

/** By default an option matches when its label contains the query, ignoring case and diacritics. */
function labelMatch(query: string, option: SelectOption) {
  return fold(option.label).includes(fold(query.trim()))
}

/**
 * A Select for long lists: the same trigger, but the list opens with a search field on top
 * (the ARIA combobox pattern — arrows move, Enter picks, Escape closes).
 */
export function SearchSelect({
  label,
  value,
  options,
  onChange,
  placeholder,
  empty,
  match = labelMatch,
}: {
  label: string
  value: string
  options: SelectOption[]
  onChange: (value: string) => void
  placeholder: string
  /** Shown when nothing matches the search. */
  empty: string
  match?: (query: string, option: SelectOption) => boolean
}) {
  const id = useId()
  const [open, setOpen] = useState(false)
  const [query, setQuery] = useState('')
  const [active, setActive] = useState(0)
  const shown = query.trim() ? options.filter((o) => match(query, o)) : options
  const current = options.find((o) => o.value === value)

  useEffect(() => {
    if (open) document.getElementById(`${id}-${active}`)?.scrollIntoView({ block: 'nearest' })
  }, [open, active, id])

  const choose = (option: SelectOption | undefined) => {
    if (!option) return
    onChange(option.value)
    setOpen(false)
  }

  return (
    <Popover.Root
      open={open}
      onOpenChange={(next) => {
        setOpen(next)
        if (next) {
          setQuery('')
          setActive(Math.max(0, options.findIndex((o) => o.value === value)))
        }
      }}
    >
      <Popover.Trigger className="inline-flex h-8 cursor-pointer items-center gap-1.5 touch-target rounded-lg border border-rule bg-paper px-2.5 hover:border-ink-2">
        <span className="text-ink-2">{label}:</span> <span>{current?.label}</span>
        <span className="text-ink-2" aria-hidden="true">
          ▾
        </span>
      </Popover.Trigger>
      <Popover.Portal>
        <Popover.Content
          className="z-[1000] w-[max(var(--radix-popover-trigger-width),280px)] rounded-lg border border-rule bg-paper p-1 shadow-float"
          align="start"
          sideOffset={4}
        >
          <input
            className="mb-1 h-8 w-full rounded-md border border-rule bg-paper px-2.5 text-ink outline-none placeholder:text-ink-2 focus:border-route focus:shadow-[0_0_0_1px_var(--route)]"
            type="search"
            role="combobox"
            aria-label={label}
            aria-expanded="true"
            aria-controls={`${id}-list`}
            aria-autocomplete="list"
            aria-activedescendant={shown[active] ? `${id}-${active}` : undefined}
            placeholder={placeholder}
            value={query}
            onChange={(event) => {
              setQuery(event.target.value)
              setActive(0)
            }}
            onKeyDown={(event) => {
              const last = shown.length - 1
              const move: Record<string, number> = { ArrowDown: Math.min(active + 1, last), ArrowUp: Math.max(active - 1, 0), Home: 0, End: last }
              if (event.key in move) {
                event.preventDefault()
                setActive(Math.max(0, move[event.key]))
              } else if (event.key === 'Enter') {
                event.preventDefault()
                choose(shown[active])
              }
            }}
          />
          <ul id={`${id}-list`} role="listbox" aria-label={label} className="m-0 max-h-[320px] list-none overflow-y-auto p-0">
            {shown.map((o, i) => (
              <li
                key={o.value}
                id={`${id}-${i}`}
                role="option"
                aria-selected={o.value === value}
                className={cn('flex cursor-pointer justify-between gap-3 rounded-md px-2.5 py-1.5 touch-target', i === active && 'bg-route-soft')}
                onMouseMove={() => setActive(i)}
                onClick={() => choose(o)}
              >
                {o.label}
                {o.value === value && <span className="text-route">✓</span>}
              </li>
            ))}
            {shown.length === 0 && <li className="px-2.5 py-1.5 text-sm text-ink-2">{empty}</li>}
          </ul>
        </Popover.Content>
      </Popover.Portal>
    </Popover.Root>
  )
}
