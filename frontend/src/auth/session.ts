// The signed-in session, outside React so the API client can read it too.
//
// The access token lives only in this module's memory, never in Web Storage, so a script
// injected into the page has nothing to read later and nothing survives the tab. The
// refresh token never reaches JavaScript at all: the API keeps it in an HttpOnly cookie
// and trades it for a new access token on POST /api/auth/refresh (see ADR 0005).

import { useSyncExternalStore } from 'react'
import type { Permission } from './permissions'

export interface SessionUser {
  id: string
  name: string
  email: string
  /** AdaPlatform permissions from Tokari, lower-case (e.g. "quality:read"). */
  permissions: string[]
}

export interface Session {
  accessToken: string
  /** ISO timestamp. */
  expiresAt: string
  user: SessionUser
}

export type SessionState =
  /** On page load: asking the API whether the refresh cookie still holds a session. */
  | { status: 'checking' }
  | { status: 'signedOut'; reason?: 'expired' | 'unavailable' }
  | { status: 'signedIn'; session: Session }

/** A failed call to /api/auth, by HTTP status (0 = no answer at all). */
export class AuthError extends Error {
  readonly status: number

  constructor(status: number) {
    super(`Sign-in failed (HTTP ${status})`)
    this.status = status
  }
}

// The API refuses cookie-authenticated calls without this header (CSRF defence).
const CSRF_HEADER = { 'X-Requested-With': 'fetch' }

// Refresh a minute before the access token runs out (Tokari issues 5-minute tokens), or a
// third of its remaining life for a shorter token, so a short lifetime never becomes a loop.
const REFRESH_AHEAD_MS = 60_000
const MIN_REFRESH_DELAY_MS = 5_000
// After a failed refresh (Tokari restarting, network down), retry with backoff:
// 10 s, 20 s, 40 s … at most every 5 minutes, and not at all while the tab is hidden.
const RETRY_FIRST_MS = 10_000
const RETRY_MAX_MS = 300_000

let state: SessionState = { status: 'checking' }
const listeners = new Set<() => void>()
let refreshTimer: ReturnType<typeof setTimeout> | undefined
let inflightRefresh: Promise<Session | null> | null = null
let retryDelay = RETRY_FIRST_MS
let retryWhenVisible = false

const msLeft = (session: Session) => new Date(session.expiresAt).getTime() - Date.now()

/** The one place a refresh gets scheduled. */
function scheduleRefresh(delay: number, { skipWhileHidden = false } = {}) {
  clearTimeout(refreshTimer)
  refreshTimer = setTimeout(() => {
    if (skipWhileHidden && document.hidden) retryWhenVisible = true
    else void refresh()
  }, delay)
}

// Tabs tell each other about sign-in and sign-out, so all of them follow.
const tabs = typeof BroadcastChannel === 'undefined' ? null : new BroadcastChannel('adaplatform.auth')
tabs?.addEventListener('message', (event: MessageEvent<'signedIn' | 'signedOut'>) => {
  if (event.data === 'signedOut') setState({ status: 'signedOut' })
  // Another tab signed in: the cookie is shared, so this tab can pick the session up.
  if (event.data === 'signedIn' && state.status !== 'signedIn') void refresh()
})

// Timers don't run while a laptop sleeps, and background tabs throttle them. Coming back,
// refresh at once if the token is due or a retry was held back, instead of letting the
// first request fail with 401.
if (typeof document !== 'undefined') {
  document.addEventListener('visibilitychange', () => {
    if (document.visibilityState !== 'visible') return
    const due = state.status === 'signedIn' && msLeft(state.session) < REFRESH_AHEAD_MS
    if (due || retryWhenVisible) {
      retryWhenVisible = false
      void refresh()
    }
  })
}

function setState(next: SessionState) {
  state = next
  clearTimeout(refreshTimer)
  if (next.status === 'signedIn') {
    const left = msLeft(next.session)
    scheduleRefresh(Math.max(left - Math.min(REFRESH_AHEAD_MS, left / 3), MIN_REFRESH_DELAY_MS))
  }
  listeners.forEach((listener) => listener())
}

export const sessionStore = {
  subscribe(listener: () => void) {
    listeners.add(listener)
    return () => listeners.delete(listener)
  },
  get: () => state,
}

export function useSession(): SessionState {
  return useSyncExternalStore(sessionStore.subscribe, sessionStore.get)
}

export function accessToken(): string | null {
  return state.status === 'signedIn' ? state.session.accessToken : null
}

export function hasPermission(session: Session, permission: Permission): boolean {
  return session.user.permissions.includes(permission)
}

export async function login(userName: string, password: string): Promise<void> {
  const response = await post('/api/auth/login', { userName, password })
  if (!response.ok) throw new AuthError(response.status)
  setState({ status: 'signedIn', session: (await response.json()) as Session })
  tabs?.postMessage('signedIn')
}

/**
 * Trades the refresh cookie for a new session. One call at a time: callers that ask while
 * a refresh runs share its result. Across tabs a Web Lock serialises refreshes too —
 * Tokari rotates refresh tokens, so two tabs refreshing with the same cookie at once would
 * end one of them.
 */
export function refresh(): Promise<Session | null> {
  inflightRefresh ??= withRefreshLock(doRefresh).finally(() => (inflightRefresh = null))
  return inflightRefresh
}

async function doRefresh(): Promise<Session | null> {
  let response: Response
  try {
    response = await post('/api/auth/refresh')
  } catch {
    return keepOrFail()
  }

  if (response.ok) {
    const session = (await response.json()) as Session
    retryDelay = RETRY_FIRST_MS
    setState({ status: 'signedIn', session })
    return session
  }
  if (response.status === 401 || response.status === 403) {
    // No session, an ended one, or access to AdaPlatform was removed.
    setState({ status: 'signedOut', reason: state.status === 'signedIn' ? 'expired' : undefined })
    return null
  }
  return keepOrFail()
}

/**
 * Tokari unreachable. The refresh cookie may well still be good, so keep trying in the
 * background: a live session stays until its token runs out; after that (or on page load)
 * the login page says the service is unavailable — and the session resumes on its own as
 * soon as a retry gets through, without asking for the password again.
 */
function keepOrFail(): Session | null {
  if (state.status !== 'signedIn' || msLeft(state.session) <= 0) setState({ status: 'signedOut', reason: 'unavailable' })
  scheduleRefresh(retryDelay, { skipWhileHidden: true })
  retryDelay = Math.min(retryDelay * 2, RETRY_MAX_MS)
  return state.status === 'signedIn' ? state.session : null
}

export async function logout(): Promise<void> {
  clearTimeout(refreshTimer)
  retryWhenVisible = false
  try {
    // Revokes the session in Tokari and clears the cookie; the local sign-out happens
    // whether or not the server answers.
    await post('/api/auth/logout')
  } finally {
    setState({ status: 'signedOut' })
    tabs?.postMessage('signedOut')
  }
}

function post(path: string, body?: unknown): Promise<Response> {
  return fetch(path, {
    method: 'POST',
    credentials: 'same-origin',
    headers: { ...CSRF_HEADER, ...(body === undefined ? {} : { 'Content-Type': 'application/json' }), Accept: 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
}

function withRefreshLock<T>(task: () => Promise<T>): Promise<T> {
  return typeof navigator !== 'undefined' && navigator.locks ? navigator.locks.request('adaplatform.refresh', task) : task()
}
