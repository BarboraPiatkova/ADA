import { useQuery } from '@tanstack/react-query'
import { Tabs } from 'radix-ui'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { NetworkMapView } from './map/NetworkMapView'
import { mapConfigQuery } from './queries'
import { DeviceHealthView } from './quality/DeviceHealthView'
import { LanguageSwitch } from './ui/LanguageSwitch'
import { QueryState } from './ui/QueryState'
import { ThemeSwitch } from './ui/ThemeSwitch'

// Two screens as Radix tabs, mirrored in the URL hash so each has a shareable link and
// the browser's back button works — without a router for two routes.
const VIEWS = ['mapa', 'jednotky'] as const
type View = (typeof VIEWS)[number]

function viewFromHash(): View {
  const hash = window.location.hash.replace(/^#\/?/, '')
  return (VIEWS as readonly string[]).includes(hash) ? (hash as View) : 'mapa'
}

export default function App() {
  const { t } = useTranslation()
  const [view, setView] = useState<View>(viewFromHash)
  const mapConfig = useQuery(mapConfigQuery)

  useEffect(() => {
    const onHash = () => setView(viewFromHash())
    window.addEventListener('hashchange', onHash)
    return () => window.removeEventListener('hashchange', onHash)
  }, [])

  return (
    <Tabs.Root className="layout" value={view} onValueChange={(v) => (window.location.hash = `/${v}`)}>
      <header className="header">
        <h1>AdaPlatform</h1>
        <Tabs.List className="tabs" aria-label={t('app.screens')}>
          {VIEWS.map((v) => (
            <Tabs.Trigger key={v} value={v} className="tab">
              {t(`app.views.${v}`)}
            </Tabs.Trigger>
          ))}
        </Tabs.List>
        <div className="header-actions">
          <LanguageSwitch />
          <ThemeSwitch />
        </div>
      </header>
      <Tabs.Content value="mapa" className="content">
        <QueryState query={mapConfig} loading={t('app.loading')}>
          {(config) => <NetworkMapView baseLayers={config.baseLayers} />}
        </QueryState>
      </Tabs.Content>
      <Tabs.Content value="jednotky" className="content">
        <DeviceHealthView />
      </Tabs.Content>
    </Tabs.Root>
  )
}
