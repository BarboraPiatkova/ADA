import 'i18next'
import type { cs } from './cs'

// Czech is the reference: t('…') keys are checked against it at compile time.
declare module 'i18next' {
  interface CustomTypeOptions {
    resources: { translation: typeof cs }
  }
}
