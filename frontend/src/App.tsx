import { Tabs } from 'radix-ui'
import { Suspense, useEffect, useMemo, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { LoginPage } from './auth/LoginPage'
import { hasPermission, useSession, type Session } from './auth/session'
import { UserMenu } from './auth/UserMenu'
import { GROUPS, SCREENS, screenPath, type GroupId, type Screen, type ScreenId } from './screens'
import { useDocumentTitle } from './ui/useDocumentTitle'
import { Empty } from './ui/Empty'
import { BrandMark } from './ui/icons'
import { LanguageSwitch } from './ui/LanguageSwitch'
import { ThemeSwitch } from './ui/ThemeSwitch'
import { pathInHash } from './navigation'

// The screens as Radix tabs, grouped (map, statistics, data, counting units) and mirrored in the URL
// hash, so each has a shareable link and the browser's back button works — without a router.

/**
 * The screen the hash names: "#/statistiky/provoz", a link from before the groups ("#/provoz"), or just a
 * group ("#/statistiky": its last-seen or first screen). Anything else: the first screen allowed.
 */
function screenFromHash(allowed: readonly Screen[], lastInGroup: Partial<Record<GroupId, ScreenId>>): ScreenId | undefined {
  const segments = pathInHash().split('/').filter(Boolean)
  const named = allowed.find((s) => s.id === segments.at(-1))
  if (named) return named.id
  const group = allowed.filter((s) => s.group === segments[0])
  const last = lastInGroup[segments[0] as GroupId]
  return (group.find((s) => s.id === last) ?? group[0] ?? allowed[0])?.id
}

/** Rewrites an old or partial hash to the screen's own path, keeping its parameters and the history entry. */
function canonicalHash(screen: ScreenId) {
  const [path, query] = window.location.hash.replace(/^#\/?/, '').split('?')
  if (path !== screenPath(screen)) window.history.replaceState(null, '', `#/${screenPath(screen)}${query ? `?${query}` : ''}`)
}

const TAB =
  'cursor-pointer border-b-[3px] border-transparent font-medium whitespace-nowrap text-ink-2 hover:text-ink data-[state=active]:border-route data-[state=active]:font-semibold data-[state=active]:text-ink'

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
  // A screen the user has no permission for isn't shown at all, nor a group left empty.
  const screens = useMemo(() => SCREENS.filter((s) => hasPermission(session, s.permission)), [session])
  const groups = useMemo(() => GROUPS.map((id) => ({ id, screens: screens.filter((s) => s.group === id) })).filter((g) => g.screens.length > 0), [screens])
  // Going back to a group opens the screen last seen there.
  const lastInGroup = useRef<Partial<Record<GroupId, ScreenId>>>({})
  const [view, setView] = useState<ScreenId | undefined>(() => screenFromHash(screens, {}))
  const group = screens.find((s) => s.id === view)?.group

  useEffect(() => {
    const onHash = () => setView(screenFromHash(screens, lastInGroup.current))
    window.addEventListener('hashchange', onHash)
    return () => window.removeEventListener('hashchange', onHash)
  }, [screens])
  useEffect(() => {
    if (!view || !group) return
    lastInGroup.current[group] = view
    canonicalHash(view)
  }, [view, group])

  // The browser tab (and history, bookmarks, screen readers) name the screen, not just the app.
  useDocumentTitle(view ? t(`app.views.${view}`) : undefined)
  const main = useRef<HTMLElement>(null)
  const openGroup = (id: string) => {
    const inGroup = groups.find((g) => g.id === id)?.screens ?? []
    const target = inGroup.find((s) => s.id === lastInGroup.current[id as GroupId]) ?? inGroup[0]
    if (target) window.location.hash = `/${screenPath(target.id)}`
  }

  return (
    <Tabs.Root className="flex h-svh flex-col" value={group} onValueChange={openGroup}>
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
          {groups.map(({ id }) => (
            <Tabs.Trigger key={id} value={id} className={`${TAB} flex-1 px-2 md:flex-none md:px-3`}>
              {t(`app.groups.${id}`)}
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
        {screens.length === 0 && <Empty>{t('auth.noPermissions')}</Empty>}
        {groups.map((g) => (
          <Tabs.Content key={g.id} value={g.id} className="flex min-h-0 flex-1 flex-col">
            <Tabs.Root className="flex min-h-0 flex-1 flex-col" value={view} onValueChange={(v) => (window.location.hash = `/${screenPath(v as ScreenId)}`)}>
              {g.screens.length > 1 && (
                <Tabs.List className="flex gap-1 overflow-x-auto border-b border-rule bg-paper px-3 touch-target md:px-5" aria-label={t(`app.groups.${g.id}`)}>
                  {g.screens.map(({ id }) => (
                    <Tabs.Trigger key={id} value={id} className={`${TAB} h-10 px-2 text-sm md:px-3`}>
                      {t(`app.views.${id}`)}
                    </Tabs.Trigger>
                  ))}
                </Tabs.List>
              )}
              {g.screens.map(({ id, Component, skeleton }) => (
                <Tabs.Content key={id} value={id} className="flex min-h-0 flex-1">
                  <Suspense fallback={skeleton(t('app.loading'))}>
                    <Component />
                  </Suspense>
                </Tabs.Content>
              ))}
            </Tabs.Root>
          </Tabs.Content>
        ))}
      </main>
    </Tabs.Root>
  )
}
