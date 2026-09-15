import { useState } from 'react'
import { Link, useNavigate } from 'react-router'
import { PageHeader } from '../layout/PageHeader'
import { roleOptions, useCreateUser, type UserInput } from './api'

const empty: UserInput = { name: '', email: '', password: '', role: 'MECHANIC' }

function Field({
  label,
  hint,
  children,
}: {
  label: string
  hint?: string
  children: React.ReactNode
}) {
  return (
    <label className="block">
      <span className="text-sm font-medium">{label}</span>
      {children}
      {hint && <span className="mt-1 block text-xs text-ink-soft">{hint}</span>}
    </label>
  )
}

const input =
  'mt-1 w-full rounded-lg border border-line bg-panel px-3 py-2 text-sm focus:border-brand focus:outline-none'

/**
 * O "Cadastro de Funcionário" do protótipo, reduzido ao que `users` guarda
 * (D-48): nome, e-mail e cargo, mais a senha inicial que a API exige. CPF,
 * telefone, admissão, salário, especialidades e foto não têm coluna — a D-01
 * deixou RH fora do MVP e a D-47 já cortou a especialidade.
 */
export function UserFormPage() {
  const navigate = useNavigate()
  const create = useCreateUser()
  const [form, setForm] = useState<UserInput>(empty)

  function change<K extends keyof UserInput>(key: K, value: UserInput[K]) {
    setForm((current) => ({ ...current, [key]: value }))
  }

  async function submit(event: React.FormEvent) {
    event.preventDefault()
    await create.mutateAsync({
      ...form,
      name: form.name.trim(),
      email: form.email.trim(),
    })
    navigate('/mechanics')
  }

  return (
    <section>
      <PageHeader icon="wrench" title="Cadastro de Funcionário" />

      <form onSubmit={submit} className="max-w-2xl rounded-xl bg-panel p-6 shadow-sm">
        <h2 className="text-sm font-semibold uppercase text-ink-soft">Dados pessoais &amp; contato</h2>

        <div className="mt-3 grid gap-4 sm:grid-cols-2">
          <div className="sm:col-span-2">
            <Field label="Nome completo">
              <input
                className={input}
                value={form.name}
                placeholder="Nome do colaborador"
                minLength={2}
                maxLength={120}
                required
                autoFocus
                onChange={(event) => change('name', event.target.value)}
              />
            </Field>
          </div>

          <Field label="E-mail" hint="É o login do funcionário no sistema.">
            <input
              className={input}
              type="email"
              value={form.email}
              placeholder="colaborador@syspitstops.com"
              maxLength={160}
              required
              autoComplete="off"
              onChange={(event) => change('email', event.target.value)}
            />
          </Field>

          {/* O protótipo não desenha a senha, mas sem ela não há primeiro
              acesso: é a inicial, que o funcionário troca quando existir tela
              para isso. */}
          <Field label="Senha inicial" hint="Mínimo 8 caracteres.">
            <input
              className={input}
              type="password"
              value={form.password}
              minLength={8}
              maxLength={100}
              required
              autoComplete="new-password"
              onChange={(event) => change('password', event.target.value)}
            />
          </Field>
        </div>

        <h2 className="mt-6 text-sm font-semibold uppercase text-ink-soft">Contrato &amp; cargo</h2>

        <div className="mt-3 grid gap-4 sm:grid-cols-2">
          <Field label="Cargo / Função" hint="Define o que o funcionário enxerga e pode fazer.">
            <select
              className={input}
              value={form.role}
              onChange={(event) => change('role', event.target.value as UserInput['role'])}
            >
              {roleOptions.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
          </Field>
        </div>

        {create.isError && (
          <p role="alert" className="mt-4 rounded-lg bg-red-50 p-3 text-sm text-red-700">
            {(create.error as Error).message}
          </p>
        )}

        <div className="mt-6 flex flex-wrap items-center justify-end gap-3">
          <Link to="/mechanics" className="rounded-lg border border-line px-4 py-2 text-sm">
            Cancelar
          </Link>
          <button
            type="submit"
            disabled={create.isPending}
            className="rounded-lg bg-brand px-6 py-2 text-sm font-semibold text-white disabled:opacity-50"
          >
            {create.isPending ? 'Cadastrando…' : 'Cadastrar Funcionário'}
          </button>
        </div>
      </form>
    </section>
  )
}
