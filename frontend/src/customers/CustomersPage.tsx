import { useEffect, useState } from 'react'
import { Link } from 'react-router'
import { PageHeader } from '../layout/PageHeader'
import { PlusCircleIcon } from '../layout/icons'
import { pageSize, useCustomers, useSetCustomerActive, type Customer } from './api'

/** "Fiat Strada 2021" — the year is optional in the database. */
function describeVehicle(vehicle: Customer['vehicles'][number]): string {
  return [vehicle.brand, vehicle.model, vehicle.modelYear].filter(Boolean).join(' ')
}

function initialsOf(name: string): string {
  const parts = name.trim().split(/\s+/)
  const first = parts.at(0)?.[0] ?? ''
  const last = parts.length > 1 ? (parts.at(-1)?.[0] ?? '') : ''
  return (first + last).toUpperCase()
}

function CustomerCard({ customer }: { customer: Customer }) {
  const setActive = useSetCustomerActive()

  return (
    <li className="flex flex-col items-center rounded-lg bg-surface p-4 text-center">
      <span
        aria-hidden="true"
        className="flex size-12 items-center justify-center rounded-full bg-brand text-sm font-semibold text-white"
      >
        {initialsOf(customer.name)}
      </span>

      <p className="mt-3 font-semibold">{customer.name}</p>

      {customer.vehicles.length === 0 ? (
        <p className="mt-1 text-xs text-ink-soft">Sem veículo cadastrado</p>
      ) : (
        <ul className="mt-1 space-y-0.5 text-xs text-ink-soft">
          {customer.vehicles.map((vehicle) => (
            <li key={vehicle.id} title={vehicle.plate}>
              {describeVehicle(vehicle)}
            </li>
          ))}
        </ul>
      )}

      {!customer.isActive && (
        <span className="mt-2 rounded bg-line px-2 py-0.5 text-xs text-ink-soft">inativo</span>
      )}

      <div className="mt-3 flex flex-col items-center gap-1.5">
        {/* Histórico ainda não tem tela nem endpoint; entra com a API da OS. */}
        <button
          type="button"
          disabled
          title="Disponível quando a API de ordens de serviço existir"
          className="rounded bg-brand px-3 py-1 text-xs font-medium text-white disabled:opacity-40"
        >
          Histórico
        </button>
        <Link
          to={`/customers/${customer.id}`}
          className="rounded bg-sky-500 px-3 py-1 text-xs font-medium text-white"
        >
          Editar
        </Link>
        <button
          type="button"
          disabled={setActive.isPending}
          onClick={() => setActive.mutate({ id: customer.id, active: !customer.isActive })}
          className="text-xs text-ink-soft underline underline-offset-2 disabled:opacity-50"
        >
          {customer.isActive ? 'Desativar' : 'Reativar'}
        </button>
      </div>
    </li>
  )
}

export function CustomersPage() {
  const [search, setSearch] = useState('')
  const [term, setTerm] = useState('')
  const [includeInactive, setIncludeInactive] = useState(false)
  const [page, setPage] = useState(1)

  // The attendant types with the customer on the phone; firing a request per
  // keystroke would put the list behind the typing.
  useEffect(() => {
    const timer = setTimeout(() => {
      setTerm(search)
      setPage(1)
    }, 300)
    return () => clearTimeout(timer)
  }, [search])

  const { data, isPending, isError, error } = useCustomers({ search: term, includeInactive, page })
  const total = data?.total ?? 0
  const lastPage = Math.max(1, Math.ceil(total / pageSize))

  return (
    <section>
      <PageHeader
        icon="users"
        title={includeInactive ? 'Todos os clientes:' : 'Clientes com registro ativo:'}
        actions={
          <Link
            to="/customers/new"
            className="inline-flex items-center gap-2 rounded-lg bg-brand px-5 py-2.5 text-sm font-semibold text-white hover:bg-brand/90"
          >
            <PlusCircleIcon className="h-5 w-5" />
            Cadastrar novo cliente
          </Link>
        }
      />

      <div className="mt-4 flex flex-wrap items-center justify-between gap-3">
        <div className="flex flex-wrap items-center gap-3">
          <input
            type="search"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder="Buscar por nome ou telefone"
            aria-label="Buscar clientes"
            className="w-full max-w-sm rounded border border-line bg-panel px-3 py-2 text-sm"
          />
          <label className="flex items-center gap-2 text-sm text-ink-soft">
            <input
              type="checkbox"
              checked={includeInactive}
              onChange={(event) => {
                setIncludeInactive(event.target.checked)
                setPage(1)
              }}
            />
            Mostrar inativos
          </label>
        </div>
      </div>

      {isError && (
        <p role="alert" className="mt-4 rounded border border-line bg-panel p-4 text-sm">
          {(error as Error).message}
        </p>
      )}

      <div className="mt-4 rounded-xl border border-line bg-panel p-4 md:p-6">
        {isPending ? (
          <p className="text-sm text-ink-soft">Carregando…</p>
        ) : data && data.items.length === 0 ? (
          <p className="py-8 text-center text-sm text-ink-soft">
            {term ? `Nenhum cliente encontrado para “${term}”.` : 'Nenhum cliente cadastrado ainda.'}
          </p>
        ) : (
          <ul className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-5">
            {data?.items.map((customer) => (
              <CustomerCard key={customer.id} customer={customer} />
            ))}
          </ul>
        )}
      </div>

      {total > pageSize && (
        <div className="mt-4 flex items-center justify-between text-sm">
          <span className="text-ink-soft">
            Página {page} de {lastPage} · {total} clientes
          </span>
          <div className="flex gap-2">
            <button
              type="button"
              disabled={page <= 1}
              onClick={() => setPage((current) => current - 1)}
              className="rounded border border-line px-3 py-1 disabled:opacity-40"
            >
              Anterior
            </button>
            <button
              type="button"
              disabled={page >= lastPage}
              onClick={() => setPage((current) => current + 1)}
              className="rounded border border-line px-3 py-1 disabled:opacity-40"
            >
              Próxima
            </button>
          </div>
        </div>
      )}
    </section>
  )
}
