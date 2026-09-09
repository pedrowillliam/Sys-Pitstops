import { useEffect, useRef, useState } from 'react'
import { NavLink, Outlet, useNavigate } from 'react-router'
import { useLogout, useSession } from '../auth/session'
import { visibleTo } from './navigation'
import type { NavItem } from './navigation'
import { ChevronDownIcon, MenuIcon, SearchIcon } from './icons'
import { icons } from './iconMap'
import logoSimbolo from '../assets/logo-simbolo.png'
import logoTexto from '../assets/logo-texto.png'

const roleLabel: Record<string, string> = {
  ADMIN: 'Administrador',
  ATTENDANT: 'Atendente',
  MECHANIC: 'Mecânico',
}

/**
 * The chrome of the prototype: a navy top bar with the logo on a white card, a
 * global search and the signed-in user, plus a floating dark rail of icons on
 * the left. The rail collapses into a drawer below `lg`, since the desktop
 * frames in Figma are 1728 wide and the mechanic's screens are a separate set.
 */
export function AppShell() {
  const { data: user } = useSession()
  const logout = useLogout()
  const navigate = useNavigate()
  const [drawerOpen, setDrawerOpen] = useState(false)

  if (!user) {
    return null
  }

  const items = visibleTo(user.role ?? '')
  const main = items.filter((item) => !item.atFoot)
  const foot = items.filter((item) => item.atFoot)

  return (
    <div className="flex min-h-full flex-col">
      <TopBar
        userName={user.name}
        role={user.role ?? ''}
        onSignOut={async () => {
          await logout.mutateAsync()
          navigate('/login', { replace: true })
        }}
        signingOut={logout.isPending}
        onOpenDrawer={() => setDrawerOpen(true)}
      />

      <div className="flex flex-1 gap-6 px-4 py-6 lg:px-8">
        {/* Rail fixo do desenho. Acompanha a rolagem para continuar alcançável
            em listas longas, que é o que o Kanban e o estoque produzem. */}
        <nav
          aria-label="Seções"
          className="sticky top-6 hidden h-fit shrink-0 flex-col items-center gap-3 rounded-[28px] bg-brand-deep px-4 py-5 lg:flex"
        >
          {main.map((item) => (
            <RailLink key={item.to} item={item} />
          ))}
          {foot.length > 0 && (
            <>
              <span className="my-1 h-px w-10 bg-white/15" />
              {foot.map((item) => (
                <RailLink key={item.to} item={item} labelHidden />
              ))}
            </>
          )}
        </nav>

        <main className="min-w-0 flex-1">
          <Outlet />
        </main>
      </div>

      {drawerOpen && (
        <Drawer items={items} onClose={() => setDrawerOpen(false)} />
      )}
    </div>
  )
}

function RailLink({ item, labelHidden = false }: { item: NavItem; labelHidden?: boolean }) {
  const Icon = icons[item.icon]

  return (
    <NavLink
      to={item.to}
      title={labelHidden ? item.label : undefined}
      className={({ isActive }) =>
        [
          'flex w-[86px] flex-col items-center gap-1.5 rounded-2xl px-2 py-3 text-center transition',
          isActive
            ? 'bg-white text-brand-deep shadow-sm'
            : 'text-white/80 hover:bg-white/10 hover:text-white',
        ].join(' ')
      }
    >
      <Icon className="h-7 w-7" />
      {!labelHidden && (
        <span className="text-[10px] leading-tight font-semibold tracking-wide uppercase">
          {item.label}
          {!item.ready && <span className="block font-normal opacity-60">em breve</span>}
        </span>
      )}
    </NavLink>
  )
}

function TopBar({
  userName,
  role,
  onSignOut,
  signingOut,
  onOpenDrawer,
}: {
  userName: string
  role: string
  onSignOut: () => void
  signingOut: boolean
  onOpenDrawer: () => void
}) {
  return (
    <header className="sticky top-0 z-30 flex h-[76px] shrink-0 items-center gap-4 bg-brand pr-4 text-white lg:pr-8">
      <div className="flex h-full shrink-0 items-center gap-2 rounded-br-[24px] bg-white px-4 lg:px-6">
        <img src={logoSimbolo} alt="" className="h-9 w-auto" />
        <img src={logoTexto} alt="Sys Pitstops" className="hidden h-5 w-auto sm:block" />
      </div>

      <button
        type="button"
        onClick={onOpenDrawer}
        className="rounded-lg p-2 text-white/90 hover:bg-white/10 lg:hidden"
        aria-label="Abrir menu"
      >
        <MenuIcon className="h-6 w-6" />
      </button>

      <GlobalSearch />

      <div className="ml-auto flex items-center gap-3">
        <OnlinePill />
        <span className="hidden h-8 w-px bg-white/25 sm:block" />
        <UserMenu userName={userName} role={role} onSignOut={onSignOut} signingOut={signingOut} />
      </div>
    </header>
  )
}

/**
 * "Buscar por placa ou cliente…", as the prototype labels it. It lands on the
 * board with the term applied, which is where an order is looked up from — the
 * API filters by plate or customer name on /api/service-orders.
 */
function GlobalSearch() {
  const navigate = useNavigate()
  const [term, setTerm] = useState('')

  return (
    <form
      role="search"
      className="mx-auto hidden w-full max-w-[420px] md:block"
      onSubmit={(event) => {
        event.preventDefault()
        const trimmed = term.trim()
        navigate(trimmed ? `/service-orders?search=${encodeURIComponent(trimmed)}` : '/service-orders')
      }}
    >
      <label className="relative block">
        <span className="sr-only">Buscar por placa ou cliente</span>
        <SearchIcon className="pointer-events-none absolute top-1/2 left-3 h-5 w-5 -translate-y-1/2 text-ink-soft" />
        <input
          type="search"
          value={term}
          onChange={(event) => setTerm(event.target.value)}
          placeholder="Buscar por placa ou cliente…"
          className="w-full rounded-lg bg-white py-2.5 pr-3 pl-10 text-sm text-ink placeholder:text-ink-soft focus:outline-2 focus:outline-offset-2 focus:outline-white"
        />
      </label>
    </form>
  )
}

/**
 * The prototype draws a green "Online" pill. It shows the browser's real
 * connectivity rather than a decoration: the PWA is meant to be opened in a
 * workshop yard, where losing signal is the normal case, not the exception.
 */
function OnlinePill() {
  const [online, setOnline] = useState(() => navigator.onLine)

  useEffect(() => {
    const update = () => setOnline(navigator.onLine)
    window.addEventListener('online', update)
    window.addEventListener('offline', update)
    return () => {
      window.removeEventListener('online', update)
      window.removeEventListener('offline', update)
    }
  }, [])

  return (
    <span
      className={`hidden items-center gap-1.5 rounded-full px-3 py-1 text-xs font-semibold sm:inline-flex ${
        online ? 'bg-online/20 text-white' : 'bg-red-500/25 text-white'
      }`}
    >
      <span className={`h-2 w-2 rounded-full ${online ? 'bg-online' : 'bg-red-400'}`} />
      {online ? 'Online' : 'Offline'}
    </span>
  )
}

function UserMenu({
  userName,
  role,
  onSignOut,
  signingOut,
}: {
  userName: string
  role: string
  onSignOut: () => void
  signingOut: boolean
}) {
  const [open, setOpen] = useState(false)
  const box = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!open) {
      return
    }

    function close(event: MouseEvent) {
      if (!box.current?.contains(event.target as Node)) {
        setOpen(false)
      }
    }

    document.addEventListener('mousedown', close)
    return () => document.removeEventListener('mousedown', close)
  }, [open])

  return (
    <div ref={box} className="relative">
      <button
        type="button"
        onClick={() => setOpen((value) => !value)}
        aria-expanded={open}
        className="flex items-center gap-2 rounded-lg py-1 pr-1 pl-2 hover:bg-white/10"
      >
        <span className="grid h-9 w-9 place-items-center rounded-full bg-white/20 text-sm font-bold">
          {initials(userName)}
        </span>
        <span className="hidden text-left leading-tight sm:block">
          <span className="block text-sm font-semibold">{userName}</span>
          <span className="block text-xs text-white/70">{roleLabel[role] ?? role}</span>
        </span>
        <ChevronDownIcon className="h-4 w-4 text-white/70" />
      </button>

      {open && (
        <div className="absolute right-0 z-40 mt-2 w-52 overflow-hidden rounded-xl bg-panel py-1 text-ink shadow-lg ring-1 ring-line">
          <p className="px-3 py-2 text-xs text-ink-soft sm:hidden">
            {userName} · {roleLabel[role] ?? role}
          </p>
          <button
            type="button"
            onClick={onSignOut}
            disabled={signingOut}
            className="w-full px-3 py-2 text-left text-sm hover:bg-surface disabled:opacity-50"
          >
            {signingOut ? 'Saindo…' : 'Sair'}
          </button>
        </div>
      )}
    </div>
  )
}

function Drawer({
  items,
  onClose,
}: {
  items: NavItem[]
  onClose: () => void
}) {
  return (
    <div className="fixed inset-0 z-40 lg:hidden">
      <button
        type="button"
        aria-label="Fechar menu"
        className="absolute inset-0 bg-black/50"
        onClick={onClose}
      />
      <nav
        aria-label="Seções"
        className="absolute inset-y-0 left-0 flex w-64 flex-col gap-1 bg-brand-deep p-4 text-white"
      >
        {items.map((item) => {
          const Icon = icons[item.icon]
          return (
            <NavLink
              key={item.to}
              to={item.to}
              onClick={onClose}
              className={({ isActive }) =>
                `flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm ${
                  isActive ? 'bg-white font-semibold text-brand-deep' : 'text-white/80 hover:bg-white/10'
                }`
              }
            >
              <Icon className="h-5 w-5" />
              {item.label}
              {!item.ready && (
                <span className="ml-auto text-[10px] tracking-wide uppercase opacity-60">
                  em breve
                </span>
              )}
            </NavLink>
          )
        })}
      </nav>
    </div>
  )
}

function initials(name: string): string {
  const parts = name.trim().split(/\s+/)
  const first = parts[0]?.[0] ?? '?'
  const last = parts.length > 1 ? parts[parts.length - 1][0] : ''
  return (first + last).toUpperCase()
}
