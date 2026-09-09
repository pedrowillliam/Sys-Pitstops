import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { PageHeader } from '../layout/PageHeader'
import {
  formatQuantity,
  usePart,
  useSavePart,
  useSetPartActive,
  type Part,
  type PartInput,
} from './api'

const empty: PartInput = {
  sku: '',
  name: '',
  description: '',
  unit: 'UN',
  salePrice: 0,
  costPrice: 0,
  minQuantity: 0,
  location: '',
}

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

export function PartFormPage() {
  const { id } = useParams<{ id: string }>()
  const existing = usePart(id)

  if (id && existing.isPending) {
    return <p className="text-sm text-ink-soft">Carregando…</p>
  }

  if (id && existing.isError) {
    return (
      <p role="alert" className="text-sm">
        {(existing.error as Error).message}
      </p>
    )
  }

  const part = existing.data
  const initial: PartInput = part
    ? {
        sku: part.sku,
        name: part.name,
        description: part.description ?? '',
        unit: part.unit,
        salePrice: part.salePrice,
        costPrice: part.costPrice,
        minQuantity: part.minQuantity,
        location: part.location ?? '',
      }
    : empty

  // Keyed so switching parts remounts the form with the new values, instead of
  // syncing props into state through an effect.
  return <PartForm key={id ?? 'new'} id={id} initial={initial} balance={part} />
}

function PartForm({
  id,
  initial,
  balance,
}: {
  id?: string
  initial: PartInput
  balance?: Part
}) {
  const navigate = useNavigate()
  const editing = Boolean(id)
  const save = useSavePart(id)
  const setActive = useSetPartActive()
  const [form, setForm] = useState<PartInput>(initial)

  function change<K extends keyof PartInput>(key: K, value: PartInput[K]) {
    setForm((current) => ({ ...current, [key]: value }))
  }

  async function submit(event: React.FormEvent) {
    event.preventDefault()
    await save.mutateAsync({
      ...form,
      description: form.description?.trim() || undefined,
      location: form.location?.trim() || undefined,
    })
    navigate('/inventory')
  }

  return (
    <section>
      <PageHeader icon="box" title={editing ? 'Editar peça' : 'Cadastrar nova peça'} />

      <form onSubmit={submit} className="max-w-2xl rounded-xl bg-panel p-6 shadow-sm">
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Código (SKU)">
            <input
              className={input}
              value={form.sku}
              maxLength={40}
              required
              onChange={(event) => change('sku', event.target.value)}
            />
          </Field>

          <Field label="Unidade" hint="UN, L, KG, PC…">
            <input
              className={input}
              value={form.unit}
              maxLength={10}
              required
              onChange={(event) => change('unit', event.target.value)}
            />
          </Field>

          <div className="sm:col-span-2">
            <Field label="Nome">
              <input
                className={input}
                value={form.name}
                maxLength={160}
                required
                onChange={(event) => change('name', event.target.value)}
              />
            </Field>
          </div>

          <Field label="Preço de venda (R$)">
            <input
              className={input}
              type="number"
              min={0}
              step="0.01"
              value={form.salePrice}
              required
              onChange={(event) => change('salePrice', Number(event.target.value))}
            />
          </Field>

          <Field label="Preço de custo (R$)">
            <input
              className={input}
              type="number"
              min={0}
              step="0.01"
              value={form.costPrice}
              required
              onChange={(event) => change('costPrice', Number(event.target.value))}
            />
          </Field>

          <Field label="Estoque mínimo" hint="Abaixo disto a peça aparece como estoque mínimo.">
            <input
              className={input}
              type="number"
              min={0}
              step="0.001"
              value={form.minQuantity}
              required
              onChange={(event) => change('minQuantity', Number(event.target.value))}
            />
          </Field>

          <Field label="Localização" hint="Prateleira, gaveta, corredor.">
            <input
              className={input}
              value={form.location ?? ''}
              maxLength={60}
              onChange={(event) => change('location', event.target.value)}
            />
          </Field>

          <div className="sm:col-span-2">
            <Field label="Descrição">
              <textarea
                className={`${input} min-h-24`}
                value={form.description ?? ''}
                maxLength={1000}
                onChange={(event) => change('description', event.target.value)}
              />
            </Field>
          </div>
        </div>

        {/* O saldo não é campo aqui de propósito: ele é um cache de
            stock_movements e só muda por lançamento (seção 4 do data-model). */}
        <p className="mt-4 rounded-lg bg-surface p-3 text-xs text-ink-soft">
          {editing ? (
            <>
              Saldo atual:{' '}
              <strong className="text-ink">
                {formatQuantity(balance?.quantityOnHand ?? 0)} {form.unit}
              </strong>
              . O saldo não se edita por aqui — use <strong>Movimentar</strong> na lista, para que
              cada mudança deixe um registro de movimentação.
            </>
          ) : (
            <>
              A peça é criada com saldo zero. Depois de salvar, use{' '}
              <strong>Movimentar</strong> na lista para lançar a entrada — é o lançamento que põe a
              peça na prateleira.
            </>
          )}
        </p>

        {save.isError && (
          <p role="alert" className="mt-4 rounded-lg bg-red-50 p-3 text-sm text-red-700">
            {(save.error as Error).message}
          </p>
        )}

        <div className="mt-6 flex flex-wrap items-center justify-end gap-3">
          {editing && balance && (
            <button
              type="button"
              disabled={setActive.isPending}
              onClick={async () => {
                await setActive.mutateAsync({
                  id: id as string,
                  active: !balance.isActive,
                })
                navigate('/inventory')
              }}
              className="mr-auto text-sm text-ink-soft underline underline-offset-2 disabled:opacity-50"
            >
              {balance.isActive ? 'Desativar peça' : 'Reativar peça'}
            </button>
          )}

          <Link to="/inventory" className="rounded-lg border border-line px-4 py-2 text-sm">
            Cancelar
          </Link>
          <button
            type="submit"
            disabled={save.isPending}
            className="rounded-lg bg-brand px-6 py-2 text-sm font-semibold text-white disabled:opacity-50"
          >
            {save.isPending ? 'Salvando…' : 'Salvar'}
          </button>
        </div>
      </form>
    </section>
  )
}
