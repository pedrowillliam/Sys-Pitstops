import { useState } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router'
import { landingFor } from '../layout/navigation'
import { useLogin, useSession } from './session'
import logoSimbolo from '../assets/logo-simbolo.png'
import logoTexto from '../assets/logo-texto.png'

/**
 * A tela "Login - ADMINISTRADOR" do Figma, que é a única com código extraído
 * (`design/figma/html/login-administrador.tsx`) — daí as medidas exatas: cartão
 * de 500px com raio de 30, campos de 50px de altura com raio de 10, rótulos em
 * #1e3a8a a 20px e o botão ocupando os 80px de baixo do cartão.
 *
 * Três controles do desenho ficaram de fora por não terem nada atrás (D-43):
 * "Esqueceu o e-mail/senha?", "Lembre de mim." e o (i) de "Quem somos".
 */
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
    // Sem cor de fundo própria: o gradiente do desenho já está no body.
    <div className="flex min-h-full flex-col items-center justify-center px-4 py-12">
      <img src={logoSimbolo} alt="" className="h-[146px] w-auto" />
      <img src={logoTexto} alt="Sys Pitstops" className="mt-[-22px] mb-3 h-[31px] w-auto" />

      <form
        onSubmit={submit}
        noValidate
        className="w-full max-w-[500px] overflow-hidden rounded-[30px] bg-panel shadow-[0_5px_20px_rgba(0,0,0,0.25)]"
      >
        <div className="px-10 pt-9 pb-14">
          <label className="block text-xl font-semibold text-blue-900" htmlFor="email">
            Email/Usuário:
          </label>
          <input
            id="email"
            type="email"
            autoComplete="username"
            required
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            className="mt-2 h-[50px] w-full rounded-[10px] bg-panel px-4 text-base shadow-[0_3px_8px_rgba(0,0,0,0.18)] outline-none focus:ring-2 focus:ring-brand"
          />

          <label className="mt-7 block text-xl font-semibold text-blue-900" htmlFor="password">
            Senha:
          </label>
          <input
            id="password"
            type="password"
            autoComplete="current-password"
            required
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            className="mt-2 h-[50px] w-full rounded-[10px] bg-panel px-4 text-base shadow-[0_3px_8px_rgba(0,0,0,0.18)] outline-none focus:ring-2 focus:ring-brand"
          />
          {/* O desenho promete isto e a regra existe de verdade: a criação de
              usuário exige 8 caracteres (UserContracts). */}
          <p className="mt-2.5 text-base text-ink">Mínimo 8 caracteres.</p>

          {login.isError && (
            <p role="alert" className="mt-5 rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700">
              {login.error.message}
            </p>
          )}
        </div>

        {/* No Figma o botão é a base do cartão, não um botão solto dentro dele. */}
        <button
          type="submit"
          disabled={login.isPending}
          className="w-full bg-brand py-5 text-2xl font-extrabold text-white transition hover:bg-[#122c60] disabled:opacity-60"
        >
          {login.isPending ? 'Entrando…' : 'Acessar Sistema'}
        </button>
      </form>

      {/* A faixa azul que fecha a tela no desenho (1728×25 no rodapé do frame). */}
      <div aria-hidden="true" className="fixed inset-x-0 bottom-0 h-6 bg-brand" />
    </div>
  )
}
