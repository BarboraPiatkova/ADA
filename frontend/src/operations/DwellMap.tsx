import { useMemo } from 'react'
import { useTranslation } from 'react-i18next'
import type { BaseLayer, StopDwell } from '../api'
import type { Format } from '../i18n/format'
import { EXCESS_BINS, excessStep } from './shared'
import { StopValueMap } from './StopValueMap'

/** Stops coloured by how much longer vehicles stand there than their passengers need, sized by visits. */
export function DwellMap({
  layers,
  stops,
  selected,
  onSelect,
  format,
}: {
  layers: BaseLayer[]
  stops: StopDwell[]
  selected: number | null
  onSelect: (code: number) => void
  format: Format
}) {
  const { t } = useTranslation()
  const valueStops = useMemo(
    () =>
      stops.map((s) => ({
        ...s,
        size: s.visits,
        step: excessStep(s.medianExcessSeconds),
        detail: `${format.seconds(s.medianSeconds)} · ${s.medianExcessSeconds > 0 ? '+' : ''}${format.seconds(s.medianExcessSeconds)} · ${format.number(s.visits)} ${t('dwell.map.visits')}`,
      })),
    [stops, format, t],
  )
  const legend = [
    `≤ ${format.seconds(EXCESS_BINS[0])}`,
    ...EXCESS_BINS.slice(1).map((upper, i) => `${format.seconds(EXCESS_BINS[i])} – ${format.seconds(upper)}`),
    `> ${format.seconds(EXCESS_BINS[EXCESS_BINS.length - 1])}`,
  ]

  return (
    <StopValueMap
      layers={layers}
      stops={valueStops}
      selected={selected}
      onSelect={onSelect}
      title={t('dwell.map.title')}
      subtitle={t('dwell.map.subtitle')}
      legendTitle={t('dwell.map.legend')}
      legendLabels={legend}
    />
  )
}
