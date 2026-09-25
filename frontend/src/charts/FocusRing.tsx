import { FOCUS_RING } from './marks'

/** Keyboard focus ring around a chart mark: the box's outline, 2px in the product colour. */
export function FocusRing({ show, x, y, width, height }: { show: boolean; x: number; y: number; width: number; height: number }) {
  return show ? <rect x={x} y={y} width={width} height={height} rx={4} className={FOCUS_RING} /> : null
}
