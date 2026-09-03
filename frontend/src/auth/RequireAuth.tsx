import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router'
import { useSession } from './session'

export function RequireAuth({ children }: { children: ReactNode }) {
  const { data: user, isPending, isError } = useSession()
  const location = useLocation()

  if (isPending) {
    return <FullPage>Carregando…</FullPage>
  }

  // An unreachable API is not the same as being signed out. Sending the person
  // to the login screen here would let them type a password into a form that
  // cannot possibly answer.
  if (isError) {
    return <FullPage>Não foi possível falar com o servidor. Tente novamente.</FullPage>
  }

  if (!user) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  }

  return children
}

// Guards a route by role. The API refuses the same calls with 403 — this only
// keeps the person from landing on a screen that could never load.
export function RequireRole({ allowed, children }: { allowed: string[]; children: ReactNode }) {
  const { data: user } = useSession()

  if (user && allowed.length > 0 && !allowed.includes(user.role ?? '')) {
    return <Navigate to="/" replace />
  }

  return children
}

function FullPage({ children }: { children: ReactNode }) {
  return (
    <div className="flex min-h-full items-center justify-center p-8 text-center text-ink-soft">
      {children}
    </div>
  )
}
