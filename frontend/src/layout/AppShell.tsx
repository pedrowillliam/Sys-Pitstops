import { useState } from 'react'
import { NavLink, Outlet, useNavigate } from 'react-router'
import { useLogout, useSession } from '../auth/session'
import { visibleTo } from './navigation'

const roleLabel: Record<string, string> = {
  ADMIN: 'Administrador',
  ATTENDANT: 'Atendente',
  MECHANIC: 'Mecânico',
}

export function AppShell() {
  const { data: user } = useSession()
  const logout = useLogout()
  const navigate = useNavigate()
  const [menuOpen, setMenuOpen] = useState(false)

  if (!user) {
    return null
  }

  const items = visibleTo(user.role ?? '')

  async function signOut() {
    await logout.mutateAsync()
    navigate('/login', { replace: true })
  }

  return (
    <div className="flex min-h-full flex-col md:flex-row">
      <header className="flex items-center justify-between bg-brand px-4 py-3 text-white md:hidden">
        <span className="font-semibold">Sys Pitstops</span>
        <button
          type="button"
          className="rounded border border-white/30 px-3 py-1 text-sm"
          aria-expanded={menuOpen}
          aria-controls="main-nav"
          onClick={() => setMenuOpen((open) => !open)}
        >
          Menu
        </button>
      </header>

      <nav
        id="main-nav"
        className={`${menuOpen ? 'block' : 'hidden'} bg-brand text-white md:block md:w-60 md:shrink-0`}
      >
        <div className="hidden px-5 py-6 md:block">
          <p className="text-lg font-semibold">Sys Pitstops</p>
          <p className="text-sm text-white/60">Oficina Piloto</p>
        </div>

        <ul className="px-3 pb-4 md:pb-0">
          {items.map((item) => (
            <li key={item.to}>
              <NavLink
                to={item.to}
                onClick={() => setMenuOpen(false)}
                className={({ isActive }) =>
                  `flex items-center justify-between rounded px-3 py-2 text-sm ${
                    isActive ? 'bg-white/15 font-medium' : 'text-white/80 hover:bg-white/10'
                  }`
                }
              >
                {item.label}
                {!item.ready && (
                  <span className="rounded bg-white/10 px-1.5 py-0.5 text-[10px] uppercase tracking-wide text-white/60">
                    em breve
                  </span>
                )}
              </NavLink>
            </li>
          ))}
        </ul>

        <div className="mt-auto px-5 py-4 text-sm md:sticky md:top-full">
          <p className="font-medium">{user.name}</p>
          <p className="text-white/60">{roleLabel[user.role ?? ''] ?? user.role}</p>
          <button
            type="button"
            onClick={signOut}
            disabled={logout.isPending}
            className="mt-2 text-white/70 underline underline-offset-2 hover:text-white disabled:opacity-50"
          >
            {logout.isPending ? 'Saindo…' : 'Sair'}
          </button>
        </div>
      </nav>

      <main className="flex-1 p-6">
        <Outlet />
      </main>
    </div>
  )
}
