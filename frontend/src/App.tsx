import { Tabs } from 'radix-ui'
import { useEffect, useState } from 'react'

// Two screens as Radix tabs, mirrored in the URL hash so each has a shareable link and
// the browser's back button works — without a router for two routes.
const VIEWS = {
  mapa: 'Mapa sítě',
  jednotky: 'Stav jednotek',
} as const
type View = keyof typeof VIEWS

function viewFromHash(): View {
  const hash = window.location.hash.replace(/^#\/?/, '')
  return hash in VIEWS ? (hash as View) : 'mapa'
}

export default function App() {
  const [view, setView] = useState<View>(viewFromHash)

  useEffect(() => {
    const onHash = () => setView(viewFromHash())
    window.addEventListener('hashchange', onHash)
    return () => window.removeEventListener('hashchange', onHash)
  }, [])

  return (
    <Tabs.Root className="layout" value={view} onValueChange={(v) => (window.location.hash = `/${v}`)}>
      <header className="header">
        <h1>AdaPlatform</h1>
        <Tabs.List className="tabs" aria-label="Obrazovky">
          {(Object.keys(VIEWS) as View[]).map((v) => (
            <Tabs.Trigger key={v} value={v} className="tab">
              {VIEWS[v]}
            </Tabs.Trigger>
          ))}
        </Tabs.List>
      </header>
      <Tabs.Content value="mapa" className="content">
        <p className="empty">Připravuje se.</p>
      </Tabs.Content>
      <Tabs.Content value="jednotky" className="content">
        <p className="empty">Připravuje se.</p>
      </Tabs.Content>
    </Tabs.Root>
  )
}
