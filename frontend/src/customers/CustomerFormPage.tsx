import { useState } from 'react'
import { useNavigate, useParams } from 'react-router'
import { useCustomer, useSaveCustomer, type CustomerInput } from './api'
import { formatPhone, onlyDigits, toPhoneDigits } from './phone'

const empty: CustomerInput = { name: '', phone: '', document: '', email: '', notes: '' }

type Field = {
  label: string
  name: keyof CustomerInput
  required?: boolean
  type?: string
  placeholder?: string
  hint?: string
  /** Texto fixo colado à esquerda do campo. Não é digitável e não é salvo. */
  prefix?: string
  /** Quando existe, o estado guarda só dígitos e isto desenha a máscara. */
  mask?: (value: string) => string
}

const fields: Field[] = [
  { label: 'Nome', name: 'name', required: true },
  {
    label: 'Telefone com DDD',
    name: 'phone',
    required: true,
    type: 'tel',
    placeholder: '(81) 99999-0000',
    // O país é fixo: PhoneNumber.IsValid só aceita número brasileiro, e o link
    // do WhatsApp (D-15) já prefixa 55. Mostrar aqui torna visível a regra que
    // o sistema sempre aplicou — mas continua fora do que é salvo.
    prefix: '+55',
    hint: 'Depois do +55 vem o DDD. Só celular recebe o orçamento por WhatsApp.',
    mask: formatPhone,
  },
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
        phone: onlyDigits(existing.data.phone),
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
        phone: onlyDigits(form.phone),
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
            <div
              className={`mt-1 flex items-center rounded border border-line text-sm focus-within:border-ink-soft ${
                field.prefix ? 'pl-3' : ''
              }`}
            >
              {field.prefix && (
                <span className="select-none pr-2 text-ink-soft">{field.prefix}</span>
              )}
              <input
                type={field.type ?? 'text'}
                required={field.required}
                placeholder={field.placeholder}
                value={field.mask ? field.mask(form[field.name] ?? '') : (form[field.name] ?? '')}
                onChange={(event) =>
                  setForm({
                    ...form,
                    [field.name]: field.mask
                      ? toPhoneDigits(event.target.value, form[field.name] ?? '')
                      : event.target.value,
                  })
                }
                className={`w-full rounded bg-transparent py-2 outline-none ${
                  field.prefix ? 'pr-3' : 'px-3'
                }`}
              />
            </div>
            {field.hint && <span className="mt-1 block text-xs text-ink-soft">{field.hint}</span>}
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
