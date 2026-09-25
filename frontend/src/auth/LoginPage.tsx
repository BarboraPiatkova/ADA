import { useMutation } from '@tanstack/react-query'
import { useId, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { cn } from '../ui/cn'
import { BrandMark, StatusIcon } from '../ui/icons'
import { LanguageSwitch } from '../ui/LanguageSwitch'
import { ThemeSwitch } from '../ui/ThemeSwitch'
import { useDocumentTitle } from '../ui/useDocumentTitle'
import { AuthError, login } from './session'

const FIELD =
  'h-10 w-full rounded-lg touch-target border border-rule bg-paper px-3 text-ink outline-none focus:border-route focus:shadow-[0_0_0_1px_var(--route)] disabled:opacity-60'

type Message = 'wrongCredentials' | 'noAccess' | 'tooManyAttempts' | 'unavailable' | 'expired'

function messageFor(error: unknown): Message {
  if (!(error instanceof AuthError)) return 'unavailable' // no answer at all
  if (error.status === 401 || error.status === 400) return 'wrongCredentials'
  if (error.status === 403) return 'noAccess'
  if (error.status === 429) return 'tooManyAttempts'
  return 'unavailable'
}

/**
 * Sign-in with a Tokari account, the same account as Herman's other applications.
 * The password goes to this app's API, which passes it to Tokari; it is never stored.
 */
export function LoginPage({ reason }: { reason?: 'expired' | 'unavailable' }) {
  const { t } = useTranslation()
  const [userName, setUserName] = useState('')
  const [password, setPassword] = useState('')
  const [showPassword, setShowPassword] = useState(false)
  const signIn = useMutation({ mutationFn: () => login(userName.trim(), password) })
  const ids = { user: useId(), password: useId(), message: useId() }
  useDocumentTitle(t('auth.title'))

  // The latest attempt's error wins; before any attempt, say why we're here.
  const message: Message | undefined = signIn.isError ? messageFor(signIn.error) : signIn.isIdle ? reason : undefined
  const isError = message !== undefined && message !== 'expired'

  const submit = (event: FormEvent) => {
    event.preventDefault()
    if (!userName.trim() || !password) return
    signIn.mutate(undefined, { onError: () => setPassword('') })
  }

  return (
    <div className="flex min-h-svh flex-col bg-surface">
      <div className="flex justify-end gap-2 p-3">
        <LanguageSwitch />
        <ThemeSwitch />
      </div>

      <main className="flex flex-1 items-start justify-center px-4 pt-[8vh] pb-12">
        <div className="w-full max-w-[380px]">
          <p className="mb-6 flex items-center gap-2.5 font-display text-2xl font-bold text-ink">
            <BrandMark />
            AdaPlatform
          </p>

          <form
            onSubmit={submit}
            noValidate
            aria-describedby={message ? ids.message : undefined}
            className="rounded-xl border border-rule bg-paper p-6 shadow-float"
          >
            <h1 className="text-xl">{t('auth.title')}</h1>
            <p className="mt-1 mb-5 text-ink-2">{t('auth.intro')}</p>

            {message && (
              <p
                id={ids.message}
                role={isError ? 'alert' : 'status'}
                className={
                  isError
                    ? 'mb-4 flex gap-2 rounded-lg bg-fault-soft px-3 py-2.5 text-fault'
                    : 'mb-4 flex gap-2 rounded-lg bg-route-soft px-3 py-2.5 text-ink'
                }
              >
                {isError && <span className="mt-px shrink-0"><StatusIcon status="Fault" size={16} /></span>}
                <span>{t(`auth.messages.${message}`)}</span>
              </p>
            )}

            <label htmlFor={ids.user} className="mb-1 block font-display text-sm font-semibold text-ink-2">
              {t('auth.userName')}
            </label>
            <input
              id={ids.user}
              className={FIELD}
              name="username"
              autoComplete="username"
              autoCapitalize="none"
              spellCheck={false}
              required
              autoFocus
              disabled={signIn.isPending}
              value={userName}
              onChange={(event) => setUserName(event.target.value)}
            />

            <label htmlFor={ids.password} className="mt-4 mb-1 block font-display text-sm font-semibold text-ink-2">
              {t('auth.password')}
            </label>
            <div className="relative">
              <input
                id={ids.password}
                className={cn(FIELD, 'pr-11')}
                type={showPassword ? 'text' : 'password'}
                name="password"
                autoComplete="current-password"
                required
                disabled={signIn.isPending}
                value={password}
                onChange={(event) => setPassword(event.target.value)}
              />
              {/* Show/hide: a long password is easier to type right when it can be checked. */}
              <button
                type="button"
                className="touch-target absolute inset-y-0 right-0 inline-flex w-10 cursor-pointer items-center justify-center rounded-r-lg text-ink-2 hover:text-ink"
                aria-label={t(showPassword ? 'auth.hidePassword' : 'auth.showPassword')}
                aria-pressed={showPassword}
                aria-controls={ids.password}
                onClick={() => setShowPassword((shown) => !shown)}
              >
                <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                  <path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12z" />
                  <circle cx="12" cy="12" r="3" />
                  {showPassword && <path d="M4 4l16 16" />}
                </svg>
              </button>
            </div>

            <button
              type="submit"
              disabled={signIn.isPending || !userName.trim() || !password}
              className="mt-6 h-10 w-full cursor-pointer touch-target rounded-lg bg-route font-display font-semibold text-on-route hover:bg-route-strong disabled:cursor-default disabled:opacity-50"
            >
              {signIn.isPending ? t('auth.submitting') : t('auth.submit')}
            </button>
          </form>

          <p className="mt-4 px-1 text-sm text-ink-2">{t('auth.help')}</p>
        </div>
      </main>
    </div>
  )
}
