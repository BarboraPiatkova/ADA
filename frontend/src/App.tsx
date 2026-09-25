import { useQuery } from '@tanstack/react-query'
import { Tabs } from 'radix-ui'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { NetworkMapView } from './map/NetworkMapView'
import { mapConfigQuery } from './queries'
import { DeviceHealthView } from './quality/DeviceHealthView'
import { BrandMark } from './ui/icons'
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
    <Tabs.Root className="flex h-svh flex-col" value={view} onValueChange={(v) => (window.location.hash = `/${v}`)}>
      {/* Phones: brand and switches on top, tabs full width below. */}
      <header className="flex flex-wrap items-center gap-x-3 border-b border-rule bg-paper px-3 pt-2 md:h-14 md:flex-nowrap md:gap-8 md:px-5 md:pt-0">
        <h1 className="flex items-center gap-2.5 font-display text-xl font-bold tracking-[0.01em] text-ink">
          <BrandMark />
          <span>AdaPlatform</span>
        </h1>
        <Tabs.List className="order-3 flex h-[42px] w-full gap-1 self-stretch md:order-none md:h-auto md:w-auto" aria-label={t('app.screens')}>
          {VIEWS.map((v) => (
            <Tabs.Trigger
              key={v}
              value={v}
              className="flex-1 cursor-pointer border-b-[3px] border-transparent px-2 font-medium whitespace-nowrap text-ink-2 hover:text-ink data-[state=active]:border-route data-[state=active]:font-semibold data-[state=active]:text-ink md:flex-none md:px-3"
            >
              {t(`app.views.${v}`)}
            </Tabs.Trigger>
          ))}
        </Tabs.List>
        <div className="ml-auto flex items-center gap-2">
          <LanguageSwitch />
          <ThemeSwitch />
        </div>
      </header>
      <Tabs.Content value="mapa" className="flex min-h-0 flex-1">
        <QueryState query={mapConfig} loading={t('app.loading')}>
          {(config) => <NetworkMapView baseLayers={config.baseLayers} />}
        </QueryState>
      </Tabs.Content>
      <Tabs.Content value="jednotky" className="flex min-h-0 flex-1">
        <DeviceHealthView />
      </Tabs.Content>
    </Tabs.Root>
  )
}
