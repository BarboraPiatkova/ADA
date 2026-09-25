import i18n from 'i18next'
import LanguageDetector from 'i18next-browser-languagedetector'
import { initReactI18next } from 'react-i18next'
import { cs } from './cs'
import { en } from './en'

export const LANGUAGES = ['cs', 'en'] as const
export type Language = (typeof LANGUAGES)[number]

/** Locale for Intl formatting of numbers and dates. */
export const LOCALES: Record<Language, string> = { cs: 'cs-CZ', en: 'en-GB' }

export function currentLanguage(): Language {
  return (LANGUAGES as readonly string[]).includes(i18n.resolvedLanguage ?? '') ? (i18n.resolvedLanguage as Language) : 'cs'
}

void i18n
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    resources: { cs: { translation: cs }, en: { translation: en } },
    supportedLngs: LANGUAGES,
    // Czech operators are the primary users; any other browser language gets Czech too.
    fallbackLng: 'cs',
    detection: {
      order: ['localStorage', 'navigator'],
      lookupLocalStorage: 'adaplatform.language',
      caches: ['localStorage'],
    },
    interpolation: { escapeValue: false }, // React already escapes
  })

// Keep <html lang> right for screen readers and hyphenation.
const setHtmlLang = (lng: string) => document.documentElement.setAttribute('lang', lng)
setHtmlLang(currentLanguage())
i18n.on('languageChanged', () => setHtmlLang(currentLanguage()))

export default i18n
