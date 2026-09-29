import { Popover } from 'radix-ui'
import { useEffect, useMemo, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { currentLanguage, LOCALES } from '../i18n'
import type { Format } from '../i18n/format'
import { cn } from './cn'

/** A period of calendar days, both ends included ("2022-08-01"); from === to is a single day. */
export interface DayRange {
  from: string
  to: string
}

// Calendar arithmetic on ISO days, in UTC so no time zone or DST shifts a day.
const toDate = (iso: string) => new Date(`${iso}T00:00:00Z`)
const toIso = (date: Date) => date.toISOString().slice(0, 10)
const addDays = (iso: string, days: number) => toIso(new Date(toDate(iso).getTime() + days * 86_400_000))
const monthOf = (iso: string) => iso.slice(0, 7)
const addMonths = (month: string, months: number) => {
  const d = toDate(`${month}-01`)
  d.setUTCMonth(d.getUTCMonth() + months)
  return toIso(d).slice(0, 7)
}

/** The weeks shown for a month, Monday first: days outside the month are null. */
function weeksOf(month: string) {
  const first = `${month}-01`
  const lead = (toDate(first).getUTCDay() + 6) % 7
  const days: (string | null)[] = Array.from({ length: lead }, () => null)
  for (let d = first; monthOf(d) === month; d = addDays(d, 1)) days.push(d)
  while (days.length % 7) days.push(null)
  return Array.from({ length: days.length / 7 }, (_, i) => days.slice(i * 7, i * 7 + 7))
}

/**
 * Picks one day or a period from a calendar. Only days with data can be picked (the others are greyed
 * out): the first click starts the period, the second ends it; the same day twice is that day alone.
 * Arrow keys move between days, Page Up/Down between months, Enter picks.
 */
export function DateRangePicker({
  label,
  days,
  value,
  onChange,
  format,
}: {
  label: string
  /** The days with data, sorted. */
  days: string[]
  /** null = every day. */
  value: DayRange | null
  onChange: (range: DayRange | null) => void
  format: Format
}) {
  const { t, i18n } = useTranslation()
  const [open, setOpen] = useState(false)
  const [anchor, setAnchor] = useState<string | null>(null)
  const [hover, setHover] = useState<string | null>(null)
  const last = days[days.length - 1]
  const [focused, setFocused] = useState(value?.from ?? last)
  const month = monthOf(focused ?? last)
  const available = useMemo(() => new Set(days), [days])
  const grid = useRef<HTMLDivElement>(null)

  const locale = LOCALES[currentLanguage()]
  const { monthTitle, weekdays } = useMemo(() => {
    const title = new Intl.DateTimeFormat(locale, { month: 'long', year: 'numeric', timeZone: 'UTC' })
    const weekday = new Intl.DateTimeFormat(locale, { weekday: 'short', timeZone: 'UTC' })
    // 2024-01-01 was a Monday.
    return { monthTitle: (m: string) => title.format(toDate(`${m}-01`)), weekdays: Array.from({ length: 7 }, (_, i) => weekday.format(toDate(addDays('2024-01-01', i)))) }
    // eslint-disable-next-line react-hooks/exhaustive-deps -- names change with the language
  }, [i18n.resolvedLanguage])

  // Keyboard focus follows the focused day, also into another month.
  useEffect(() => {
    if (open) grid.current?.querySelector<HTMLButtonElement>(`[data-day="${focused}"]`)?.focus()
  }, [open, focused])

  const summary = !value ? t('dates.allDays') : value.from === value.to ? format.date(value.from) : `${format.date(value.from)} – ${format.date(value.to)}`

  // The period drawn: the chosen one, or while picking, from the first click to the pointer.
  const shown = anchor ? { from: [anchor, hover ?? anchor].sort()[0], to: [anchor, hover ?? anchor].sort()[1] } : value

  const pick = (day: string) => {
    if (!available.has(day)) return
    if (!anchor) {
      setAnchor(day)
      return
    }
    const [from, to] = [anchor, day].sort()
    onChange({ from, to })
    setAnchor(null)
    setOpen(false)
  }

  const onKeyDown = (event: React.KeyboardEvent) => {
    const step: Record<string, () => string> = {
      ArrowLeft: () => addDays(focused, -1),
      ArrowRight: () => addDays(focused, 1),
      ArrowUp: () => addDays(focused, -7),
      ArrowDown: () => addDays(focused, 7),
      PageUp: () => `${addMonths(monthOf(focused), -1)}-01`,
      PageDown: () => `${addMonths(monthOf(focused), 1)}-01`,
    }
    if (event.key in step) {
      event.preventDefault()
      const next = step[event.key]()
      setFocused(next)
      if (anchor) setHover(next)
    }
  }

  const canBack = days.length > 0 && month > monthOf(days[0])
  const canForward = days.length > 0 && month < monthOf(last)
  const navButton = 'inline-flex size-8 cursor-pointer items-center justify-center rounded-md text-ink-2 hover:bg-surface hover:text-ink disabled:cursor-default disabled:opacity-40 disabled:hover:bg-transparent'

  return (
    <Popover.Root
      open={open}
      onOpenChange={(next) => {
        setOpen(next)
        setAnchor(null)
        setHover(null)
        if (next) setFocused(value?.from ?? last)
      }}
    >
      <Popover.Trigger className="inline-flex h-8 cursor-pointer items-center gap-1.5 touch-target rounded-lg border border-rule bg-paper px-2.5 hover:border-ink-2">
        <span className="text-ink-2">{label}:</span> <span>{summary}</span>
        <span className="text-ink-2" aria-hidden="true">
          ▾
        </span>
      </Popover.Trigger>
      <Popover.Portal>
        <Popover.Content
          className="z-[1000] w-[292px] rounded-lg border border-rule bg-paper p-3 shadow-float"
          align="start"
          sideOffset={4}
          aria-label={label}
          onOpenAutoFocus={(event) => event.preventDefault()}
        >
          <div className="mb-2 flex items-center justify-between">
            <button className={navButton} disabled={!canBack} aria-label={t('dates.previousMonth')} onClick={() => setFocused(`${addMonths(month, -1)}-01`)}>
              ‹
            </button>
            <span className="font-display font-semibold capitalize" aria-live="polite">
              {monthTitle(month)}
            </span>
            <button className={navButton} disabled={!canForward} aria-label={t('dates.nextMonth')} onClick={() => setFocused(`${addMonths(month, 1)}-01`)}>
              ›
            </button>
          </div>
          <div ref={grid} role="grid" aria-label={monthTitle(month)} onKeyDown={onKeyDown} onPointerLeave={() => setHover(null)}>
            <div role="row" className="grid grid-cols-7">
              {weekdays.map((w) => (
                <span key={w} role="columnheader" className="py-1 text-center text-xs text-ink-2">
                  {w}
                </span>
              ))}
            </div>
            {weeksOf(month).map((week, i) => (
              <div key={i} role="row" className="grid grid-cols-7">
                {week.map((day, j) => {
                  if (!day) return <span key={j} role="gridcell" />
                  const has = available.has(day)
                  const inRange = shown !== null && day >= shown.from && day <= shown.to
                  const isEnd = shown !== null && (day === shown.from || day === shown.to)
                  return (
                    <span key={day} role="gridcell" className={cn('py-0.5', inRange && 'bg-route-soft', day === shown?.from && 'rounded-l-md', day === shown?.to && 'rounded-r-md')}>
                      <button
                        data-day={day}
                        tabIndex={day === focused ? 0 : -1}
                        aria-disabled={!has}
                        aria-selected={inRange}
                        aria-label={has ? format.date(day) : t('dates.noData', { day: format.date(day) })}
                        className={cn(
                          'mx-auto flex size-8 items-center justify-center rounded-md text-sm tabular-nums',
                          has ? 'cursor-pointer hover:bg-surface-2' : 'cursor-default text-ink-2 line-through decoration-rule',
                          isEnd && has && 'bg-route text-on-route hover:bg-route-strong',
                        )}
                        onClick={() => {
                          setFocused(day)
                          pick(day)
                        }}
                        onPointerEnter={() => anchor && setHover(day)}
                        onKeyDown={(event) => {
                          if (event.key === 'Enter' || event.key === ' ') {
                            event.preventDefault()
                            pick(day)
                          }
                        }}
                      >
                        {Number(day.slice(8))}
                      </button>
                    </span>
                  )
                })}
              </div>
            ))}
          </div>
          <p className="mt-2 text-xs text-ink-2">{anchor ? t('dates.pickEnd', { day: format.date(anchor) }) : t('dates.hint')}</p>
          <div className="mt-2 flex flex-wrap gap-2 border-t border-rule pt-2">
            <button
              className="inline-flex h-8 cursor-pointer items-center rounded-md border border-rule px-2.5 text-sm hover:border-ink-2"
              onClick={() => {
                onChange(null)
                setOpen(false)
              }}
            >
              {t('dates.allDays')}
            </button>
            {last && (
              <button
                className="inline-flex h-8 cursor-pointer items-center rounded-md border border-rule px-2.5 text-sm hover:border-ink-2"
                onClick={() => {
                  onChange({ from: last, to: last })
                  setOpen(false)
                }}
              >
                {t('dates.lastDay')}
              </button>
            )}
          </div>
        </Popover.Content>
      </Popover.Portal>
    </Popover.Root>
  )
}
