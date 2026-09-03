import { useState } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router'
import { landingFor } from '../layout/navigation'
import { useLogin, useSession } from './session'

export function LoginPage() {
  const { data: user, isPending } = useSession()
  const login = useLogin()
  const navigate = useNavigate()
  const location = useLocation()

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')

  if (isPending) {
    return null
  }

  if (user) {
    const from = (location.state as { from?: string } | null)?.from
    return <Navigate to={from ?? landingFor(user.role ?? '')} replace />
  }

  async function submit(event: React.FormEvent) {
    event.preventDefault()

    const signedIn = await login.mutateAsync({ email, password }).catch(() => null)
    if (signedIn) {
      const from = (location.state as { from?: string } | null)?.from
      navigate(from ?? landingFor(signedIn.role ?? ''), { replace: true })
    }
  }

  return (
    <div className="flex min-h-full items-center justify-center bg-brand p-6">
      <form
        onSubmit={submit}
        className="w-full max-w-sm rounded-lg bg-panel p-6 shadow-lg"
        noValidate
      >
        <h1 className="text-xl font-semibold">Sys Pitstops</h1>
        <p className="mt-1 text-sm text-ink-soft">Entre para acessar a oficina.</p>

        <label className="mt-6 block text-sm font-medium" htmlFor="email">
          E-mail
        </label>
        <input
          id="email"
          type="email"
          autoComplete="username"
          required
          value={email}
          onChange={(event) => setEmail(event.target.value)}
          className="mt-1 w-full rounded border border-line px-3 py-2 outline-none focus:border-ink"
        />

        <label className="mt-4 block text-sm font-medium" htmlFor="password">
          Senha
        </label>
        <input
          id="password"
          type="password"
          autoComplete="current-password"
          required
          value={password}
          onChange={(event) => setPassword(event.target.value)}
          className="mt-1 w-full rounded border border-line px-3 py-2 outline-none focus:border-ink"
        />

        {login.isError && (
          <p role="alert" className="mt-4 rounded bg-red-50 px-3 py-2 text-sm text-red-700">
            {login.error.message}
          </p>
        )}

        <button
          type="submit"
          disabled={login.isPending}
          className="mt-6 w-full rounded bg-ink py-2 font-medium text-white disabled:opacity-60"
        >
          {login.isPending ? 'Entrando…' : 'Entrar'}
        </button>
      </form>
    </div>
  )
}
