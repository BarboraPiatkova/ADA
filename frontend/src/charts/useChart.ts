import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react'

/** Width of an element, kept current with a ResizeObserver — charts draw to the space they get. */
export function useElementWidth<T extends HTMLElement>() {
  const ref = useRef<T>(null)
  const [width, setWidth] = useState(0)
  useEffect(() => {
    const element = ref.current
    if (!element) return
    const observer = new ResizeObserver(([entry]) => setWidth(Math.floor(entry.contentRect.width)))
    observer.observe(element)
    return () => observer.disconnect()
  }, [])
  return [ref, width] as const
}

export interface TooltipState {
  x: number
  y: number
  content: ReactNode
}

/**
 * One tooltip per chart, at the pointer in window coordinates (drawn position: fixed, so it never
 * makes the chart's scrollable box grow scrollbars). Marks call show() on pointer move and focus;
 * content is React nodes, so data is always text-escaped. `box` stays on the chart box for callers.
 */
export function useTooltip() {
  const [tooltip, setTooltip] = useState<TooltipState | null>(null)
  const box = useRef<HTMLDivElement>(null)

  const show = useCallback((event: { clientX: number; clientY: number } | DOMRect, content: ReactNode) => {
    const point = 'clientX' in event ? { x: event.clientX, y: event.clientY } : { x: event.x + event.width / 2, y: event.y }
    setTooltip({ x: point.x, y: point.y, content })
  }, [])
  const hide = useCallback(() => setTooltip(null), [])

  return { box, tooltip, show, hide }
}
