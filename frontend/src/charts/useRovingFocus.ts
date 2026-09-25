import { useRef, useState } from 'react'

/** A focusable mark, addressed by row and column; a one-row chart just uses row 0. */
export type MarkKey = readonly [row: number, col: number]

const id = ([row, col]: MarkKey) => `${row}:${col}`

/**
 * Roving tabindex for the marks of one chart (WAI-ARIA "composite widget" pattern).
 * The whole chart is a single Tab stop — without it, a chart of 50 bars puts 50 stops
 * between the filters and the table. Inside, the arrow keys move: Left/Right along a row,
 * Up/Down between rows (keeping the column where it can), Home/End to a row's ends.
 * Enter or Space activates the mark.
 *
 * `marks` lists every focusable mark in reading order. The Tab stop is the last focused
 * mark, or the first one when that mark is gone (a filter can remove a bar).
 */
export function useRovingFocus(marks: readonly MarkKey[]) {
  const elements = useRef(new Map<string, SVGElement>())
  const [active, setActive] = useState<string | null>(null)
  /** Mark with keyboard focus (shows the ring); null while focus is elsewhere or came from a click. */
  const [focused, setFocused] = useState<string | null>(null)

  const ids = marks.map(id)
  const tabStop = active && ids.includes(active) ? active : ids[0]

  // Focusing the element runs its onFocus, which makes it the Tab stop.
  const moveTo = (mark: MarkKey | undefined) => {
    if (mark) elements.current.get(id(mark))?.focus()
  }

  const onKeyDown = (row: number, col: number, activate?: () => void) => (event: React.KeyboardEvent) => {
    const rows = [...new Set(marks.map(([r]) => r))]
    const colsOf = (r: number) => marks.filter(([mr]) => mr === r).map(([, c]) => c)
    const cols = colsOf(row)
    const i = cols.indexOf(col)
    const r = rows.indexOf(row)
    const inRow = (target: number | undefined): MarkKey | undefined => {
      if (target === undefined) return undefined
      const targetCols = colsOf(target)
      return [target, targetCols.filter((c) => c <= col).pop() ?? targetCols[0]]
    }

    switch (event.key) {
      case 'ArrowRight':
        moveTo(i < cols.length - 1 ? [row, cols[i + 1]] : undefined)
        break
      case 'ArrowLeft':
        moveTo(i > 0 ? [row, cols[i - 1]] : undefined)
        break
      case 'ArrowDown':
        moveTo(inRow(rows[r + 1]))
        break
      case 'ArrowUp':
        moveTo(inRow(rows[r - 1]))
        break
      case 'Home':
        moveTo([row, cols[0]])
        break
      case 'End':
        moveTo([row, cols[cols.length - 1]])
        break
      case 'Enter':
      case ' ':
        if (!activate) return
        activate()
        break
      default:
        return
    }
    event.preventDefault()
  }

  /**
   * Props for one focusable mark; it must be one of `marks`. `activate` runs on Enter/Space;
   * `onFocus`/`onBlur` run alongside the hook's own (e.g. to show and hide a tooltip).
   */
  const itemProps = (
    row: number,
    col: number,
    { activate, onFocus, onBlur }: { activate?: () => void; onFocus?: (el: SVGElement) => void; onBlur?: () => void } = {},
  ) => {
    const key = id([row, col])
    return {
      ref: (el: SVGElement | null) => {
        if (el) elements.current.set(key, el)
        else elements.current.delete(key)
      },
      tabIndex: tabStop === key ? 0 : -1,
      onKeyDown: onKeyDown(row, col, activate),
      onFocus: (event: React.FocusEvent<SVGElement>) => {
        setActive(key)
        setFocused(event.currentTarget.matches(':focus-visible') ? key : null)
        onFocus?.(event.currentTarget)
      },
      onBlur: () => {
        setFocused(null)
        onBlur?.()
      },
    }
  }

  const isFocused = (row: number, col: number) => focused === id([row, col])

  return { itemProps, isFocused }
}
