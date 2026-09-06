import { useState } from 'react'
import { useNavigate, useParams } from 'react-router'
import { useCustomer, useSaveCustomer, type CustomerInput } from './api'

const empty: CustomerInput = { name: '', phone: '', document: '', email: '', notes: '' }

type Field = { label: string; name: keyof CustomerInput; required?: boolean; type?: string }

const fields: Field[] = [
  { label: 'Nome', name: 'name', required: true },
  { label: 'Telefone', name: 'phone', required: true, type: 'tel' },
  { label: 'CPF ou CNPJ', name: 'document' },
  { label: 'E-mail', name: 'email', type: 'email' },
]

export function CustomerFormPage() {
  const { id } = useParams()
  const existing = useCustomer(id)

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

  const initial: CustomerInput = existing.data
    ? {
        name: existing.data.name,
        phone: existing.data.phone,
        document: existing.data.document ?? '',
        email: existing.data.email ?? '',
        notes: existing.data.notes ?? '',
      }
    : empty

  // Keyed so switching customers remounts the form with the new values,
  // instead of syncing props into state through an effect.
  return <CustomerForm key={id ?? 'new'} id={id} initial={initial} />
}

function CustomerForm({ id, initial }: { id?: string; initial: CustomerInput }) {
  const navigate = useNavigate()
  const save = useSaveCustomer(id)
  const [form, setForm] = useState(initial)

  // Empty optional fields go as null, not "": the column is nullable and an
  // empty string would make "has no document" look like "has a blank one".
  function submit(event: React.FormEvent) {
    event.preventDefault()
    save.mutate(
      {
        name: form.name.trim(),
        phone: form.phone.trim(),
        document: form.document?.trim() || null,
        email: form.email?.trim() || null,
        notes: form.notes?.trim() || null,
      },
      { onSuccess: () => navigate('/customers') },
    )
  }

  return (
    <section className="mx-auto max-w-2xl">
      <h1 className="text-2xl font-semibold">{id ? 'Editar cliente' : 'Novo cliente'}</h1>

      <form onSubmit={submit} className="mt-6 space-y-4 rounded border border-line bg-panel p-6">
        {fields.map((field) => (
          <label key={field.name} className="block">
            <span className="text-sm font-medium">
              {field.label}
              {field.required && <span aria-hidden="true"> *</span>}
            </span>
            <input
              type={field.type ?? 'text'}
              required={field.required}
              value={form[field.name] ?? ''}
              onChange={(event) => setForm({ ...form, [field.name]: event.target.value })}
              className="mt-1 w-full rounded border border-line px-3 py-2 text-sm"
            />
          </label>
        ))}

        <label className="block">
          <span className="text-sm font-medium">Observações</span>
          <textarea
            rows={3}
            value={form.notes ?? ''}
            onChange={(event) => setForm({ ...form, notes: event.target.value })}
            className="mt-1 w-full rounded border border-line px-3 py-2 text-sm"
          />
        </label>

        {save.isError && (
          <p role="alert" className="text-sm">
            {(save.error as Error).message}
          </p>
        )}

        <div className="flex gap-3 pt-2">
          <button
            type="submit"
            disabled={save.isPending}
            className="rounded bg-brand px-4 py-2 text-sm font-medium text-white disabled:opacity-50"
          >
            {save.isPending ? 'Salvando…' : 'Salvar'}
          </button>
          <button
            type="button"
            onClick={() => navigate('/customers')}
            className="rounded border border-line px-4 py-2 text-sm"
          >
            Cancelar
          </button>
        </div>
      </form>
    </section>
  )
}
