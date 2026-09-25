import { useTranslation } from 'react-i18next'
import { currentLanguage, LANGUAGES } from '../i18n'
import { SegmentedItem, SegmentedRoot } from './Segmented'

export function LanguageSwitch() {
  const { t, i18n } = useTranslation()
  return (
    <SegmentedRoot type="single" aria-label={t('language.label')} value={currentLanguage()} onValueChange={(value) => value && void i18n.changeLanguage(value)}>
      {LANGUAGES.map((lng) => (
        <SegmentedItem key={lng} value={lng} lang={lng}>
          {lng.toUpperCase()}
        </SegmentedItem>
      ))}
    </SegmentedRoot>
  )
}
