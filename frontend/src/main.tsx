import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { Tooltip } from 'radix-ui'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { ApiError } from './api'
import App from './App.tsx'
import { refresh, sessionStore } from './auth/session'
import './i18n'
import './index.css'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      // Operational data changes only when a new import runs — no need to refetch
      // every time the window regains focus.
      staleTime: 5 * 60 * 1000,
      refetchOnWindowFocus: false,
      // Not signed in or not allowed won't change by asking again.
      retry: (failures, error) => !(error instanceof ApiError && (error.status === 401 || error.status === 403)) && failures < 1,
    },
  },
})

// Signed out (here or in another tab): drop every cached response, so the next person to
// sign in on this browser never sees the previous one's data.
sessionStore.subscribe(() => {
  if (sessionStore.get().status === 'signedOut') queryClient.clear()
})

// A session may survive in the refresh cookie from an earlier visit.
void refresh()

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <Tooltip.Provider delayDuration={300}>
        <App />
      </Tooltip.Provider>
    </QueryClientProvider>
  </StrictMode>,
)
