import { ToggleGroup } from 'radix-ui'
import { useTranslation } from 'react-i18next'
import { currentLanguage, LANGUAGES } from '../i18n'

export function LanguageSwitch() {
  const { t, i18n } = useTranslation()
  return (
    <ToggleGroup.Root
      type="single"
      className="segmented"
      aria-label={t('language.label')}
      value={currentLanguage()}
      onValueChange={(value) => value && void i18n.changeLanguage(value)}
    >
      {LANGUAGES.map((lng) => (
        <ToggleGroup.Item key={lng} value={lng} className="segmented-item" lang={lng}>
          {lng.toUpperCase()}
        </ToggleGroup.Item>
      ))}
    </ToggleGroup.Root>
  )
}
