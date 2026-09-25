import { useQuery } from '@tanstack/react-query'
import { Tabs } from 'radix-ui'
import { useEffect, useMemo, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { LoginPage } from './auth/LoginPage'
import { hasPermission, useSession, type Session } from './auth/session'
import { UserMenu } from './auth/UserMenu'
import { NetworkMapView } from './map/NetworkMapView'
import { mapConfigQuery } from './queries'
import { DeviceHealthView } from './quality/DeviceHealthView'
import { useDocumentTitle } from './ui/useDocumentTitle'
import { Empty } from './ui/Empty'
import { BrandMark } from './ui/icons'
import { LanguageSwitch } from './ui/LanguageSwitch'
import { QueryState } from './ui/QueryState'
import { ThemeSwitch } from './ui/ThemeSwitch'

// Two screens as Radix tabs, mirrored in the URL hash so each has a shareable link and
// the browser's back button works — without a router for two routes.
const VIEWS = ['mapa', 'jednotky'] as const
type View = (typeof VIEWS)[number]

/** The permission each screen needs; a screen the user can't open isn't shown at all. */
const VIEW_PERMISSION: Record<View, string> = {
  mapa: 'network:read',
  jednotky: 'quality:read',
}

function viewFromHash(allowed: readonly View[]): View | undefined {
  const hash = window.location.hash.replace(/^#\/?/, '')
  return allowed.includes(hash as View) ? (hash as View) : allowed[0]
}

export default function App() {
  const { t } = useTranslation()
  const session = useSession()

  if (session.status === 'checking') {
    return (
      <div className="flex h-svh bg-surface">
        <Empty>{t('auth.checking')}</Empty>
      </div>
    )
  }
  if (session.status === 'signedOut') {
    return <LoginPage reason={session.reason} />
  }
  return <Shell session={session.session} />
}

function Shell({ session }: { session: Session }) {
  const { t } = useTranslation()
  const views = useMemo(() => VIEWS.filter((v) => hasPermission(session, VIEW_PERMISSION[v])), [session])
  const [view, setView] = useState<View | undefined>(() => viewFromHash(views))
  const mapConfig = useQuery({ ...mapConfigQuery, enabled: views.includes('mapa') })

  useEffect(() => {
    const onHash = () => setView(viewFromHash(views))
    window.addEventListener('hashchange', onHash)
    return () => window.removeEventListener('hashchange', onHash)
  }, [views])

  // The browser tab (and history, bookmarks, screen readers) name the screen, not just the app.
  useDocumentTitle(view ? t(`app.views.${view}`) : undefined)
  const main = useRef<HTMLElement>(null)

  return (
    <Tabs.Root className="flex h-svh flex-col" value={view} onValueChange={(v) => (window.location.hash = `/${v}`)}>
      {/* First Tab stop: jump past the header. A button-like link, because the URL hash is the router. */}
      <a
        href="#main"
        onClick={(event) => {
          event.preventDefault()
          main.current?.focus()
        }}
        className="sr-only z-[2000] rounded-lg bg-route px-3 py-2 font-semibold text-on-route focus:not-sr-only focus:fixed focus:top-2 focus:left-2"
      >
        {t('app.skipToContent')}
      </a>
      {/* Phones: brand and switches on top, tabs full width below. */}
      <header className="flex flex-wrap items-center gap-x-3 border-b border-rule bg-paper px-3 pt-2 md:h-14 md:flex-nowrap md:gap-8 md:px-5 md:pt-0">
        {/* The brand, not a heading: each screen's own title is its h1. */}
        <p className="flex items-center gap-2.5 font-display text-xl font-bold tracking-[0.01em] text-ink">
          <BrandMark />
          <span>AdaPlatform</span>
        </p>
        <Tabs.List className="order-3 flex h-[42px] w-full gap-1 self-stretch touch-target md:order-none md:h-auto md:w-auto" aria-label={t('app.screens')}>
          {views.map((v) => (
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
          <UserMenu user={session.user} />
        </div>
      </header>
      <main id="main" ref={main} tabIndex={-1} className="flex min-h-0 flex-1 flex-col outline-none">
        {views.length === 0 && <Empty>{t('auth.noPermissions')}</Empty>}
        <Tabs.Content value="mapa" className="flex min-h-0 flex-1">
          <QueryState query={mapConfig} loading={t('app.loading')}>
            {(config) => <NetworkMapView baseLayers={config.baseLayers} />}
          </QueryState>
        </Tabs.Content>
        <Tabs.Content value="jednotky" className="flex min-h-0 flex-1">
          <DeviceHealthView />
        </Tabs.Content>
      </main>
    </Tabs.Root>
  )
}
