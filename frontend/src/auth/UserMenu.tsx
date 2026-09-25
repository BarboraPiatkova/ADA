import { DropdownMenu } from 'radix-ui'
import { useTranslation } from 'react-i18next'
import { logout, type SessionUser } from './session'

/** Who is signed in, and the way out. */
export function UserMenu({ user }: { user: SessionUser }) {
  const { t } = useTranslation()
  const initials = (user.name || user.email)
    .split(/[\s.@_-]+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]!.toUpperCase())
    .join('')

  return (
    <DropdownMenu.Root>
      <DropdownMenu.Trigger
        className="inline-flex size-8 cursor-pointer items-center touch-target justify-center rounded-full bg-route-soft font-display text-sm font-bold text-route hover:shadow-[0_0_0_2px_var(--route)] data-[state=open]:shadow-[0_0_0_2px_var(--route)]"
        aria-label={t('auth.account', { name: user.name })}
        title={user.name}
      >
        {initials}
      </DropdownMenu.Trigger>
      <DropdownMenu.Portal>
        <DropdownMenu.Content
          align="end"
          sideOffset={8}
          collisionPadding={12}
          className="z-[1001] min-w-[220px] rounded-xl border border-rule bg-paper p-1.5 shadow-float"
        >
          <div className="px-2.5 pt-1.5 pb-2">
            <p className="font-semibold text-ink">{user.name}</p>
            {user.email && <p className="text-sm text-ink-2">{user.email}</p>}
          </div>
          <DropdownMenu.Separator className="my-1 h-px bg-rule" />
          <DropdownMenu.Item
            className="flex cursor-pointer items-center gap-2 rounded-md px-2.5 py-2 text-ink touch-target outline-none data-[highlighted]:bg-surface"
            onSelect={() => void logout()}
          >
            <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
              <path d="M15 4h4v16h-4M10 8l-4 4 4 4M6 12h10" />
            </svg>
            {t('auth.signOut')}
          </DropdownMenu.Item>
        </DropdownMenu.Content>
      </DropdownMenu.Portal>
    </DropdownMenu.Root>
  )
}
