import { useEffect, useState } from 'react'
import { Link } from 'react-router'
import { PageHeader } from '../layout/PageHeader'
import { BoxIcon, PlusCircleIcon } from '../layout/icons'
import { MovementDialog } from './MovementDialog'
import {
  formatMoney,
  formatQuantity,
  pageSize,
  stockLabel,
  useParts,
  type Part,
} from './api'

function PartRow({ part, onMove }: { part: Part; onMove: (part: Part) => void }) {
  const label = stockLabel(part)

  return (
    <li className="flex flex-wrap items-center gap-4 rounded-lg border border-line px-4 py-3">
      <span
        aria-hidden="true"
        className="grid size-10 shrink-0 place-items-center rounded-full bg-surface text-ink-soft"
      >
        <BoxIcon className="h-5 w-5" />
      </span>

      <div className="min-w-0 flex-1">
        <p className="truncate text-sm text-ink-soft">
          <span className="text-ink">{part.name}</span>
          <span className="font-semibold"> - Cód. {part.sku}</span>
          {!part.isActive && (
            <span className="ml-2 rounded bg-line px-1.5 py-0.5 text-xs text-ink-soft">inativa</span>
          )}
        </p>

        <p className="mt-1 flex flex-wrap items-center gap-2 text-sm">
          <span className="font-bold">{formatMoney(part.salePrice)}</span>
          <span className={`rounded px-2 py-0.5 text-xs font-medium ${label.className}`}>
            {label.text}
          </span>
          <span className="text-ink-soft">
            Qtd: {formatQuantity(part.quantityOnHand)} {part.unit}
          </span>
          {part.location && <span className="text-xs text-ink-soft">· {part.location}</span>}
        </p>
      </div>

      <div className="flex shrink-0 items-center gap-2">
        {/* O desenho só traz "Editar". Sem um lançamento de movimento a peça
            nunca entra no estoque, porque o saldo não é campo de formulário
            (seção 4 do data-model). */}
        <button
          type="button"
          onClick={() => onMove(part)}
          className="rounded border border-line px-3 py-1 text-xs font-medium text-ink hover:bg-surface"
        >
          Movimentar
        </button>
        <Link
          to={`/inventory/${part.id}`}
          className="rounded bg-blue-500 px-4 py-1 text-xs font-medium text-white hover:bg-blue-600"
        >
          Editar
        </Link>
      </div>
    </li>
  )
}

export function InventoryPage() {
  const [search, setSearch] = useState('')
  const [term, setTerm] = useState('')
  const [lowStock, setLowStock] = useState(false)
  const [includeInactive, setIncludeInactive] = useState(false)
  const [page, setPage] = useState(1)
  const [moving, setMoving] = useState<Part | null>(null)

  useEffect(() => {
    const timer = setTimeout(() => {
      setTerm(search)
      setPage(1)
    }, 300)
    return () => clearTimeout(timer)
  }, [search])

  const { data, isPending, isError, error } = useParts({
    search: term,
    lowStock,
    includeInactive,
    page,
  })

  const total = data?.total ?? 0
  const lastPage = Math.max(1, Math.ceil(total / pageSize))

  return (
    <section>
      <PageHeader
        icon="box"
        title="Estoque"
        actions={
          <Link
            to="/inventory/new"
            className="inline-flex items-center gap-2 rounded-lg bg-brand px-5 py-2.5 text-sm font-semibold text-white hover:bg-brand/90"
          >
            <PlusCircleIcon className="h-5 w-5" />
            Cadastrar nova peça
          </Link>
        }
      />

      <div className="mb-4 flex flex-wrap items-center gap-4">
        <input
          type="search"
          value={search}
          onChange={(event) => setSearch(event.target.value)}
          placeholder="Buscar por nome ou código"
          aria-label="Buscar peças"
          className="w-full max-w-sm rounded-lg border border-line bg-panel px-3 py-2 text-sm"
        />
        <label className="flex items-center gap-2 text-sm text-ink-soft">
          <input
            type="checkbox"
            checked={lowStock}
            onChange={(event) => {
              setLowStock(event.target.checked)
              setPage(1)
            }}
          />
          Só estoque baixo
        </label>
        <label className="flex items-center gap-2 text-sm text-ink-soft">
          <input
            type="checkbox"
            checked={includeInactive}
            onChange={(event) => {
              setIncludeInactive(event.target.checked)
              setPage(1)
            }}
          />
          Mostrar inativas
        </label>
      </div>

      {isError && (
        <p role="alert" className="mb-4 rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700">
          {(error as Error).message}
        </p>
      )}

      <div className="rounded-xl bg-panel p-4 shadow-sm md:p-6">
        <h2 className="mb-4 font-semibold">Estoque de Peças</h2>

        {isPending ? (
          <p className="text-sm text-ink-soft">Carregando…</p>
        ) : data && data.items.length === 0 ? (
          <p className="py-8 text-center text-sm text-ink-soft">
            {term
              ? `Nenhuma peça encontrada para “${term}”.`
              : lowStock
                ? 'Nenhuma peça no estoque mínimo.'
                : 'Nenhuma peça cadastrada ainda.'}
          </p>
        ) : (
          <ul className="space-y-3">
            {data?.items.map((part) => (
              <PartRow key={part.id} part={part} onMove={setMoving} />
            ))}
          </ul>
        )}
      </div>

      {total > pageSize && (
        <div className="mt-4 flex items-center justify-between text-sm">
          <span className="text-ink-soft">
            Página {page} de {lastPage} · {total} peças
          </span>
          <div className="flex gap-2">
            <button
              type="button"
              disabled={page <= 1}
              onClick={() => setPage((current) => current - 1)}
              className="rounded border border-line bg-panel px-3 py-1 disabled:opacity-40"
            >
              Anterior
            </button>
            <button
              type="button"
              disabled={page >= lastPage}
              onClick={() => setPage((current) => current + 1)}
              className="rounded border border-line bg-panel px-3 py-1 disabled:opacity-40"
            >
              Próxima
            </button>
          </div>
        </div>
      )}

      {moving && <MovementDialog part={moving} onClose={() => setMoving(null)} />}
    </section>
  )
}
