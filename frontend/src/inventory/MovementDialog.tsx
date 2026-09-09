import { useEffect, useRef, useState } from 'react'
import {
  formatQuantity,
  movementLabel,
  useMoveStock,
  usePartMovements,
  type MovementType,
  type Part,
} from './api'

const types: MovementType[] = ['IN', 'OUT', 'ADJUSTMENT']

/**
 * Where the balance actually changes. The prototype has no such control — its
 * stock screen only offers "Editar" — but the balance is a cache of
 * stock_movements and no form may write it directly, so recording the movement
 * is the only way a part ever gets onto the shelf (D-42).
 */
export function MovementDialog({ part, onClose }: { part: Part; onClose: () => void }) {
  const [type, setType] = useState<MovementType>('IN')
  const [quantity, setQuantity] = useState('')
  const [unitCost, setUnitCost] = useState('')
  const [note, setNote] = useState('')

  const move = useMoveStock(part.id)
  const history = usePartMovements(part.id)
  const dialog = useRef<HTMLDivElement>(null)

  useEffect(() => {
    function onKey(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        onClose()
      }
    }

    document.addEventListener('keydown', onKey)
    return () => document.removeEventListener('keydown', onKey)
  }, [onClose])

  const counted = type === 'ADJUSTMENT'
  const parsed = Number(quantity.replace(',', '.'))
  const valid = quantity !== '' && Number.isFinite(parsed) && (counted ? parsed >= 0 : parsed > 0)

  async function submit(event: React.FormEvent) {
    event.preventDefault()
    if (!valid) {
      return
    }

    const cost = unitCost.trim() === '' ? undefined : Number(unitCost.replace(',', '.'))

    await move.mutateAsync({
      movementType: type,
      quantity: parsed,
      unitCost: Number.isFinite(cost as number) ? cost : undefined,
      note: note.trim() || undefined,
    })

    onClose()
  }

  return (
    <div className="fixed inset-0 z-50 grid place-items-center p-4">
      <button
        type="button"
        aria-label="Fechar"
        className="absolute inset-0 bg-black/50"
        onClick={onClose}
      />

      <div
        ref={dialog}
        role="dialog"
        aria-modal="true"
        aria-labelledby="movimento-titulo"
        className="relative max-h-[90vh] w-full max-w-lg overflow-y-auto rounded-2xl bg-panel p-6 shadow-xl"
      >
        <h2 id="movimento-titulo" className="text-lg font-semibold">
          Movimentar estoque
        </h2>
        <p className="mt-1 text-sm text-ink-soft">
          {part.name} · Cód. {part.sku} · saldo atual{' '}
          <strong className="text-ink">
            {formatQuantity(part.quantityOnHand)} {part.unit}
          </strong>
        </p>

        <form onSubmit={submit} className="mt-5 space-y-4">
          <fieldset>
            <legend className="text-sm font-medium">Tipo</legend>
            <div className="mt-2 flex gap-2">
              {types.map((option) => (
                <label
                  key={option}
                  className={`flex-1 cursor-pointer rounded-lg border px-3 py-2 text-center text-sm ${
                    type === option
                      ? 'border-brand bg-brand text-white'
                      : 'border-line hover:bg-surface'
                  }`}
                >
                  <input
                    type="radio"
                    name="tipo"
                    className="sr-only"
                    checked={type === option}
                    onChange={() => setType(option)}
                  />
                  {movementLabel[option]}
                </label>
              ))}
            </div>
          </fieldset>

          <label className="block">
            <span className="text-sm font-medium">
              {counted ? 'Saldo contado' : 'Quantidade'}
            </span>
            <input
              type="number"
              min={counted ? 0 : 0.001}
              step="0.001"
              value={quantity}
              onChange={(event) => setQuantity(event.target.value)}
              required
              className="mt-1 w-full rounded-lg border border-line bg-panel px-3 py-2 text-sm"
            />
            <span className="mt-1 block text-xs text-ink-soft">
              {counted
                ? 'O ajuste registra quanto foi contado na prateleira, e o saldo passa a ser esse número.'
                : type === 'IN'
                  ? 'Quanto entrou. O saldo sobe.'
                  : 'Quanto saiu. O saldo desce.'}
            </span>
          </label>

          {type === 'IN' && (
            <label className="block">
              <span className="text-sm font-medium">Custo unitário (opcional)</span>
              <input
                type="number"
                min={0}
                step="0.01"
                value={unitCost}
                onChange={(event) => setUnitCost(event.target.value)}
                className="mt-1 w-full rounded-lg border border-line bg-panel px-3 py-2 text-sm"
              />
            </label>
          )}

          <label className="block">
            <span className="text-sm font-medium">Observação (opcional)</span>
            <input
              type="text"
              value={note}
              maxLength={500}
              onChange={(event) => setNote(event.target.value)}
              placeholder="Nota fiscal, fornecedor, motivo do ajuste…"
              className="mt-1 w-full rounded-lg border border-line bg-panel px-3 py-2 text-sm"
            />
          </label>

          {move.isError && (
            <p role="alert" className="rounded-lg bg-red-50 p-3 text-sm text-red-700">
              {(move.error as Error).message}
            </p>
          )}

          <div className="flex justify-end gap-2 pt-1">
            <button
              type="button"
              onClick={onClose}
              className="rounded-lg border border-line px-4 py-2 text-sm"
            >
              Cancelar
            </button>
            <button
              type="submit"
              disabled={!valid || move.isPending}
              className="rounded-lg bg-brand px-5 py-2 text-sm font-semibold text-white disabled:opacity-50"
            >
              {move.isPending ? 'Registrando…' : 'Registrar'}
            </button>
          </div>
        </form>

        <section className="mt-6 border-t border-line pt-4">
          <h3 className="text-sm font-semibold">Movimentação recente</h3>

          {history.isPending ? (
            <p className="mt-2 text-sm text-ink-soft">Carregando…</p>
          ) : history.data && history.data.items.length === 0 ? (
            <p className="mt-2 text-sm text-ink-soft">Esta peça ainda não teve movimento.</p>
          ) : (
            <ul className="mt-2 space-y-2 text-sm">
              {history.data?.items.map((movement) => (
                <li key={movement.id} className="flex items-baseline justify-between gap-3">
                  <span>
                    <strong>{movementLabel[movement.movementType]}</strong>{' '}
                    {formatQuantity(movement.quantity)} {part.unit}
                    {movement.serviceOrderNumber != null && (
                      <span className="text-ink-soft"> · OS #{movement.serviceOrderNumber}</span>
                    )}
                    {movement.note && <span className="text-ink-soft"> · {movement.note}</span>}
                  </span>
                  <time
                    dateTime={movement.createdAt}
                    className="shrink-0 text-xs text-ink-soft"
                  >
                    {new Date(movement.createdAt).toLocaleDateString('pt-BR')}
                  </time>
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>
    </div>
  )
}
