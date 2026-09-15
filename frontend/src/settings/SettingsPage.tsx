import { useState } from 'react'
import { useSession } from '../auth/session'
import { formatPhone, onlyDigits, toPhoneDigits } from '../customers/phone'
import { PageHeader } from '../layout/PageHeader'
import {
  useChangePassword,
  useSaveWorkshop,
  useWorkshop,
  type Workshop,
  type WorkshopInput,
} from './api'

function Saved({ children }: { children: string }) {
  return (
    <p role="status" className="text-sm font-medium text-emerald-700">
      {children}
    </p>
  )
}

/** O nome não é decorativo: é o que o cliente lê no topo do link público do
 *  orçamento. Até esta tela existir, ele era o que o seeder escreveu. */
function WorkshopCard({ workshop, editable }: { workshop: Workshop; editable: boolean }) {
  const save = useSaveWorkshop()
  const [form, setForm] = useState<WorkshopInput>({
    name: workshop.name,
    document: workshop.document ?? '',
    phone: onlyDigits(workshop.phone ?? ''),
    address: workshop.address ?? '',
  })

  function submit(event: React.FormEvent) {
    event.preventDefault()
    save.reset()
    save.mutate(form)
  }

  if (!editable) {
    const rows: [string, string][] = [
      ['Nome', workshop.name],
      ['CPF / CNPJ', workshop.document ?? '—'],
      ['Telefone', workshop.phone ? formatPhone(workshop.phone) : '—'],
      ['Endereço', workshop.address ?? '—'],
    ]

    return (
      <section className="rounded-xl border border-line bg-panel p-5">
        <h2 className="text-base font-semibold">Dados da oficina</h2>
        <p className="mt-1 text-sm text-ink-soft">Somente o administrador pode alterar.</p>

        <dl className="mt-4 grid gap-x-6 gap-y-2 sm:grid-cols-2">
          {rows.map(([label, value]) => (
            <div key={label} className="flex justify-between gap-3 text-sm">
              <dt className="text-ink-soft">{label}</dt>
              <dd className="text-right font-medium">{value}</dd>
            </div>
          ))}
        </dl>
      </section>
    )
  }

  return (
    <section className="rounded-xl border border-line bg-panel p-5">
      <h2 className="text-base font-semibold">Dados da oficina</h2>
      <p className="mt-1 text-sm text-ink-soft">
        O nome aparece para o cliente no link público do orçamento.
      </p>

      <form onSubmit={submit} className="mt-4 space-y-4">
        <label className="block">
          <span className="text-sm font-medium">
            Nome<span aria-hidden="true"> *</span>
          </span>
          <input
            required
            maxLength={120}
            value={form.name}
            onChange={(event) => setForm({ ...form, name: event.target.value })}
            className="mt-1 w-full rounded border border-line px-3 py-2 text-sm"
          />
        </label>

        <div className="grid gap-4 sm:grid-cols-2">
          <label className="block">
            <span className="text-sm font-medium">CPF / CNPJ</span>
            <input
              maxLength={18}
              value={form.document ?? ''}
              onChange={(event) => setForm({ ...form, document: event.target.value })}
              placeholder="00.000.000/0000-00"
              className="mt-1 w-full rounded border border-line px-3 py-2 text-sm"
            />
          </label>

          <label className="block">
            <span className="text-sm font-medium">Telefone com DDD</span>
            <div className="mt-1 flex items-center rounded border border-line pl-3 text-sm focus-within:border-ink-soft">
              <span className="select-none pr-2 text-ink-soft">+55</span>
              <input
                type="tel"
                value={formatPhone(form.phone ?? '')}
                onChange={(event) =>
                  setForm({ ...form, phone: toPhoneDigits(event.target.value, form.phone ?? '') })
                }
                placeholder="(81) 3333-4444"
                className="w-full rounded bg-transparent py-2 pr-3 outline-none"
              />
            </div>
          </label>
        </div>

        <label className="block">
          <span className="text-sm font-medium">Endereço</span>
          <input
            maxLength={200}
            value={form.address ?? ''}
            onChange={(event) => setForm({ ...form, address: event.target.value })}
            className="mt-1 w-full rounded border border-line px-3 py-2 text-sm"
          />
        </label>

        {save.isError && (
          <p role="alert" className="text-sm">
            {(save.error as Error).message}
          </p>
        )}

        <div className="flex items-center gap-3 pt-1">
          <button
            type="submit"
            disabled={save.isPending}
            className="rounded bg-brand px-4 py-2 text-sm font-medium text-white disabled:opacity-50"
          >
            {save.isPending ? 'Salvando…' : 'Salvar'}
          </button>

          {save.isSuccess && <Saved>Dados da oficina salvos.</Saved>}
        </div>
      </form>
    </section>
  )
}

/** O README manda trocar a senha do admin no primeiro acesso (D-22), e até aqui
 *  não havia como — a instrução apontava para nada. */
function PasswordCard() {
  const change = useChangePassword()
  const [current, setCurrent] = useState('')
  const [next, setNext] = useState('')
  const [confirmation, setConfirmation] = useState('')

  const mismatch = confirmation.length > 0 && next !== confirmation

  function submit(event: React.FormEvent) {
    event.preventDefault()

    if (mismatch) {
      return
    }

    change.reset()
    change.mutate(
      { currentPassword: current, newPassword: next },
      {
        onSuccess: () => {
          setCurrent('')
          setNext('')
          setConfirmation('')
        },
      },
    )
  }

  return (
    <section className="rounded-xl border border-line bg-panel p-5">
      <h2 className="text-base font-semibold">Minha senha</h2>
      <p className="mt-1 text-sm text-ink-soft">Mínimo 8 caracteres.</p>

      <form onSubmit={submit} className="mt-4 space-y-4">
        <label className="block">
          <span className="text-sm font-medium">
            Senha atual<span aria-hidden="true"> *</span>
          </span>
          <input
            type="password"
            required
            autoComplete="current-password"
            value={current}
            onChange={(event) => setCurrent(event.target.value)}
            className="mt-1 w-full rounded border border-line px-3 py-2 text-sm"
          />
        </label>

        <div className="grid gap-4 sm:grid-cols-2">
          <label className="block">
            <span className="text-sm font-medium">
              Nova senha<span aria-hidden="true"> *</span>
            </span>
            <input
              type="password"
              required
              minLength={8}
              autoComplete="new-password"
              value={next}
              onChange={(event) => setNext(event.target.value)}
              className="mt-1 w-full rounded border border-line px-3 py-2 text-sm"
            />
          </label>

          <label className="block">
            <span className="text-sm font-medium">
              Repita a nova senha<span aria-hidden="true"> *</span>
            </span>
            <input
              type="password"
              required
              autoComplete="new-password"
              value={confirmation}
              onChange={(event) => setConfirmation(event.target.value)}
              className="mt-1 w-full rounded border border-line px-3 py-2 text-sm"
            />
            {/* A confirmação é só da tela: o servidor não a recebe, porque duas
                cópias do mesmo campo não são um dado, são um cuidado ao digitar. */}
            {mismatch && (
              <span className="mt-1 block text-xs text-rose-600">As senhas não conferem.</span>
            )}
          </label>
        </div>

        {change.isError && (
          <p role="alert" className="text-sm">
            {(change.error as Error).message}
          </p>
        )}

        <div className="flex items-center gap-3 pt-1">
          <button
            type="submit"
            disabled={change.isPending || mismatch}
            className="rounded bg-brand px-4 py-2 text-sm font-medium text-white disabled:opacity-50"
          >
            {change.isPending ? 'Salvando…' : 'Trocar senha'}
          </button>

          {change.isSuccess && <Saved>Senha alterada.</Saved>}
        </div>
      </form>

      {/* A sessão continua valendo: o cookie da D-20 não carrega senha, e o
          token não tem refresh para revogar (D-19). Encerrar a sessão aqui
          puniria quem fez a coisa certa. */}
      <p className="mt-4 text-xs text-ink-soft">
        Você continua conectado. A nova senha vale a partir do próximo login.
      </p>
    </section>
  )
}

export function SettingsPage() {
  const { data: user } = useSession()
  const { data: workshop, isPending, isError, error } = useWorkshop()

  return (
    <>
      <PageHeader icon="gear" title="Configurações" />

      <div className="mx-auto max-w-3xl space-y-6">
        {isPending ? (
          <p className="text-sm text-ink-soft">Carregando…</p>
        ) : isError || !workshop ? (
          <p role="alert" className="text-sm">
            {(error as Error)?.message ?? 'Não foi possível carregar a oficina.'}
          </p>
        ) : (
          <WorkshopCard
            key={workshop.id}
            workshop={workshop}
            editable={user?.role === 'ADMIN'}
          />
        )}

        <PasswordCard />
      </div>
    </>
  )
}
