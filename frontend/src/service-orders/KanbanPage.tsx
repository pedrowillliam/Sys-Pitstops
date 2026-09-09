import { useState } from 'react'
import { Link } from 'react-router'
import {
  useAllowedTransitions,
  useBoard,
  useChangeStatus,
  useMechanics,
  type ServiceOrder,
  type ServiceOrderStatus,
} from './api'
import {
  columns,
  formatDay,
  formatMoney,
  needsMechanicToAdvance,
  onTheBoard,
  statusLabels,
  yardSummary,
} from './board'

function OrderCard({ order, onMove }: { order: ServiceOrder; onMove: (o: ServiceOrder) => void }) {
  const scheduled = formatDay(order.scheduledAt)

  return (
    <li className="rounded-lg border border-line bg-panel p-3 shadow-sm">
      <p className="text-xs font-medium text-ink-soft">OS-{order.number}</p>

      <p className="mt-1 inline-block rounded border border-line px-2 py-0.5 font-mono text-xs">
        {order.vehiclePlate}
      </p>

      <p className="mt-2 text-sm font-semibold">{order.vehicleDescription}</p>
      <p className="text-xs text-ink-soft">Cliente: {order.customerName}</p>

      {/* D-35: onde quer que a OS esteja, o balcão precisa ver que o cliente
          já respondeu — a aprovação nem sempre move a ordem sozinha. */}
      {order.hasApprovedQuote && (
        <p className="mt-2 inline-block rounded bg-emerald-100 px-2 py-0.5 text-xs font-medium text-emerald-800">
          Orçamento aprovado
        </p>
      )}

      <div className="mt-2 flex items-center justify-between text-xs text-ink-soft">
        <span className="truncate">{order.mechanicName ?? 'Sem mecânico'}</span>
        {scheduled && <span>{scheduled}</span>}
      </div>

      <p className="mt-2 text-sm font-semibold">{formatMoney(order.total)}</p>

      <div className="mt-3 flex gap-2">
        <button
          type="button"
          onClick={() => onMove(order)}
          className="flex-1 rounded border border-line px-2 py-1 text-xs font-medium hover:bg-surface"
        >
          Mover
        </button>
        {/* D-36: itens, orçamento e responsável ficam na OS, não no cartão. */}
        <Link
          to={`/service-orders/${order.id}`}
          className="flex-1 rounded border border-line px-2 py-1 text-center text-xs font-medium hover:bg-surface"
        >
          Abrir
        </Link>
      </div>
    </li>
  )
}

/** The buttons come from the server (D-13): the board never decides on its own
 *  who may move what. A transition marked RequiresNote is the admin waiving the
 *  quote, so the reason is asked for before it is offered. */
function MovePanel({ order, onClose }: { order: ServiceOrder; onClose: () => void }) {
  const { data: transitions, isPending } = useAllowedTransitions(order.id)
  const changeStatus = useChangeStatus()
  const [note, setNote] = useState('')
  const [target, setTarget] = useState<ServiceOrderStatus | null>(null)

  const chosen = transitions?.find((t) => t.toStatus === target)
  const needsNote = chosen?.requiresNote ?? false

  function submit() {
    if (!target) {
      return
    }

    changeStatus.mutate(
      { id: order.id, toStatus: target, note: note || undefined, waiveApproval: needsNote },
      { onSuccess: onClose },
    )
  }

  return (
    <div className="fixed inset-0 z-10 flex items-center justify-center bg-black/40 p-4">
      <div className="w-full max-w-md rounded-lg bg-panel p-5">
        <h2 className="text-lg font-semibold">
          OS-{order.number} · {order.vehiclePlate}
        </h2>
        <p className="mt-1 text-sm text-ink-soft">
          Situação atual: {statusLabels[order.status]}
        </p>

        {/* D-38: a transição para Em Análise some da lista quando não há
            responsável. Sem isto o atendente vê só "Cancelar" e não descobre
            por quê. */}
        {needsMechanicToAdvance(order) && (
          <p className="mt-4 rounded bg-amber-50 px-3 py-2 text-sm text-amber-900">
            Para avançar para {statusLabels.IN_YARD}, a OS precisa de um mecânico
            responsável.{' '}
            <Link to={`/service-orders/${order.id}`} className="font-medium underline">
              Abrir a OS para definir
            </Link>
            .
          </p>
        )}

        {isPending ? (
          <p className="mt-4 text-sm text-ink-soft">Carregando ações…</p>
        ) : transitions && transitions.length === 0 ? (
          <p className="mt-4 text-sm text-ink-soft">
            Nenhuma ação disponível para o seu perfil nesta situação.
          </p>
        ) : (
          <div className="mt-4 space-y-2">
            {transitions?.map((transition) => (
              <label key={transition.toStatus} className="flex items-center gap-2 text-sm">
                <input
                  type="radio"
                  name="target"
                  checked={target === transition.toStatus}
                  onChange={() => setTarget(transition.toStatus)}
                />
                {statusLabels[transition.toStatus]}
                {transition.requiresNote && (
                  <span className="text-xs text-ink-soft">(exige justificativa)</span>
                )}
              </label>
            ))}
          </div>
        )}

        {target && (
          <label className="mt-4 block">
            <span className="text-sm font-medium">
              {needsNote ? 'Motivo da dispensa de aprovação' : 'Observação'}
              {needsNote && <span aria-hidden="true"> *</span>}
            </span>
            <textarea
              rows={2}
              value={note}
              onChange={(event) => setNote(event.target.value)}
              className="mt-1 w-full rounded border border-line px-3 py-2 text-sm"
            />
          </label>
        )}

        {changeStatus.isError && (
          <p role="alert" className="mt-3 text-sm">
            {(changeStatus.error as Error).message}
          </p>
        )}

        <div className="mt-5 flex gap-3">
          <button
            type="button"
            disabled={!target || changeStatus.isPending || (needsNote && !note.trim())}
            onClick={submit}
            className="rounded bg-brand px-4 py-2 text-sm font-medium text-white disabled:opacity-50"
          >
            {changeStatus.isPending ? 'Movendo…' : 'Confirmar'}
          </button>
          <button
            type="button"
            onClick={onClose}
            className="rounded border border-line px-4 py-2 text-sm"
          >
            Cancelar
          </button>
        </div>
      </div>
    </div>
  )
}

export function KanbanPage() {
  const [mechanicId, setMechanicId] = useState<string>('')
  const [moving, setMoving] = useState<ServiceOrder | null>(null)

  const { data, isPending, isError, error } = useBoard(mechanicId || undefined)
  const { data: mechanics } = useMechanics()

  const orders = (data ?? []).filter(onTheBoard)
  const tiles = yardSummary(orders)

  return (
    <section>
      <header className="rounded bg-brand px-4 py-3 text-sm font-medium text-white">
        Status do Pátio
      </header>

      <div className="mt-4 grid grid-cols-2 gap-4 lg:grid-cols-4">
        {tiles.map((tile) => (
          <div key={tile.label} className="rounded-lg border border-line bg-panel p-4">
            <p className="text-sm text-ink-soft">{tile.label}</p>
            <p className="mt-1 text-2xl font-semibold">{tile.value}</p>
          </div>
        ))}
      </div>

      <div className="mt-6 flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold">Quadro de Ordens de Serviço</h1>
          <p className="text-sm text-ink-soft">
            Monitore o fluxo de trabalho de reparos da oficina.
          </p>
        </div>

        <div className="flex items-end gap-3">
        <Link
          to="/service-orders/new"
          className="rounded bg-brand px-4 py-2 text-sm font-medium text-white"
        >
          Nova OS
        </Link>

        <label className="text-sm">
          <span className="sr-only">Filtrar por mecânico</span>
          <select
            value={mechanicId}
            onChange={(event) => setMechanicId(event.target.value)}
            className="rounded border border-line bg-panel px-3 py-2 text-sm"
          >
            <option value="">Todos os mecânicos</option>
            {mechanics?.map((mechanic) => (
              <option key={mechanic.id} value={mechanic.id}>
                {mechanic.name}
              </option>
            ))}
          </select>
        </label>
        </div>
      </div>

      {isError && (
        <p role="alert" className="mt-4 rounded border border-line bg-panel p-4 text-sm">
          {(error as Error).message}
        </p>
      )}

      {isPending ? (
        <p className="mt-6 text-sm text-ink-soft">Carregando…</p>
      ) : (
        <div className="mt-4 flex gap-4 overflow-x-auto pb-4">
          {columns.map((column) => {
            const cards = orders.filter(column.holds)

            return (
              <div key={column.key} className="w-64 shrink-0">
                <div className={`h-1 rounded-t ${column.accent}`} />
                <div className="rounded-b-lg border border-t-0 border-line bg-surface p-3">
                  <div className="flex items-center justify-between">
                    <h2 className="text-sm font-semibold">{column.label}</h2>
                    <span className="rounded bg-panel px-2 py-0.5 text-xs text-ink-soft">
                      {cards.length}
                    </span>
                  </div>

                  <ul className="mt-3 space-y-3">
                    {cards.map((order) => (
                      <OrderCard key={order.id} order={order} onMove={setMoving} />
                    ))}
                  </ul>

                  {cards.length === 0 && (
                    <p className="mt-3 text-xs text-ink-soft">Nenhuma OS aqui.</p>
                  )}
                </div>
              </div>
            )
          })}
        </div>
      )}

      {moving && <MovePanel order={moving} onClose={() => setMoving(null)} />}
    </section>
  )
}
