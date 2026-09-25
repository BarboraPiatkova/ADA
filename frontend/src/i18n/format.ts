import { useMemo } from 'react'
import { useTranslation } from 'react-i18next'
import { currentLanguage, LOCALES } from '.'

/** Number, percent and date formatters for the current UI language. */
/** The formatters for the current language; see useFormat. */
export type Format = ReturnType<typeof useFormat>

export function useFormat() {
  const { i18n } = useTranslation()
  const language = i18n.resolvedLanguage
  return useMemo(() => {
    const locale = LOCALES[currentLanguage()]
    const number = new Intl.NumberFormat(locale)
    const percent = new Intl.NumberFormat(locale, { style: 'percent', maximumFractionDigits: 1 })
    const date = new Intl.DateTimeFormat(locale, { dateStyle: 'medium' })
    const dayShort = new Intl.DateTimeFormat(locale, { weekday: 'short', day: 'numeric', month: 'numeric' })
    const percentWhole = new Intl.NumberFormat(locale, { style: 'percent', maximumFractionDigits: 0 })
    return {
      number: (value: number) => number.format(value),
      percent: (value: number) => percent.format(value),
      /** ISO date ("2022-08-01") → localized, read as a calendar day (no time-zone shift). */
      date: (iso: string) => date.format(new Date(`${iso}T00:00:00`)),
      /** Compact day label for chart axes, e.g. "po 1. 8.". */
      dayShort: (iso: string) => dayShort.format(new Date(`${iso}T00:00:00`)),
      percentWhole: (value: number) => percentWhole.format(value),
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps -- recompute when the language changes
  }, [language])
}
