import { currentLanguage, LOCALES } from '../i18n'

// Table exports for Excel. Excel splits a CSV on the list separator of the reader's locale: ";" where
// the decimal separator is a comma (Czech), "," elsewhere. A UTF-8 byte order mark makes it read
// Czech letters correctly. Numbers are written as plain digits with the locale's decimal separator,
// so Excel takes them as numbers, not text; ISO date-times lose their "T" ("2022-08-01 04:35:02"), which
// Excel reads as a date and time.

export type CsvValue = string | number | boolean | null | undefined

export interface CsvColumn<T> {
  header: string
  value: (row: T) => CsvValue
}

function separators() {
  const decimal = new Intl.NumberFormat(LOCALES[currentLanguage()]).formatToParts(1.5).find((p) => p.type === 'decimal')?.value ?? '.'
  return { decimal, list: decimal === ',' ? ';' : ',' }
}

function cell(value: CsvValue, decimal: string, list: string) {
  if (value === null || value === undefined) return ''
  const text =
    typeof value === 'number' ? String(value).replace('.', decimal) : /^\d{4}-\d\d-\d\dT\d\d:\d\d/.test(String(value)) ? String(value).replace('T', ' ') : String(value)
  return text.includes(list) || text.includes('"') || /[\r\n]/.test(text) ? `"${text.replaceAll('"', '""')}"` : text
}

/** The rows as CSV text, a header line first. */
export function toCsv<T>(columns: CsvColumn<T>[], rows: T[]) {
  const { decimal, list } = separators()
  return [columns.map((c) => cell(c.header, decimal, list)), ...rows.map((row) => columns.map((c) => cell(c.value(row), decimal, list)))]
    .map((line) => line.join(list))
    .join('\r\n')
}

/** Saves the rows as a CSV file the browser downloads. */
export function downloadCsv<T>(fileName: string, columns: CsvColumn<T>[], rows: T[]) {
  const blob = new Blob(['﻿', toCsv(columns, rows)], { type: 'text/csv;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  link.click()
  URL.revokeObjectURL(url)
}
