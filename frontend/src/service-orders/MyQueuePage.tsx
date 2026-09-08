import { useState } from 'react'
import { useSession } from '../auth/session'
import {
  useAllowedTransitions,
  useChangeStatus,
  useMyQueue,
  useOrder,
  useSaveDiagnosis,
  type ServiceOrder,
  type ServiceOrderStatus,
} from './api'
import { formatMoney, statusLabels } from './board'

const statusDot: Partial<Record<ServiceOrderStatus, string>> = {
  IN_PROGRESS: 'bg-blue-500',
  READY: 'bg-emerald-500',
  AWAITING_APPROVAL: 'bg-amber-500',
}

function QueueCard({ order, onOpen }: { order: ServiceOrder; onOpen: (id: string) => void }) {
  return (
    <li className="rounded-xl border border-line bg-panel p-4">
      <div className="flex items-center justify-between">
        <span className="rounded border border-line px-2 py-0.5 font-mono text-xs">
          {order.vehiclePlate}
        </span>
        <span className="flex items-center gap-1.5 text-xs text-ink-soft">
          <span
            aria-hidden="true"
            className={`size-2 rounded-full ${statusDot[order.status] ?? 'bg-slate-400'}`}
          />
          {statusLabels[order.status]}
        </span>
      </div>

      <p className="mt-3 text-lg font-semibold">{order.vehicleDescription}</p>
      <p className="text-sm text-ink-soft">Cliente: {order.customerName}</p>
      <p className="mt-1 text-sm font-medium">{formatMoney(order.total)}</p>

      <button
        type="button"
        onClick={() => onOpen(order.id)}
        className="mt-4 w-full rounded-lg bg-brand px-4 py-2.5 text-sm font-medium text-white"
      >
        Abrir serviço
      </button>
    </li>
  )
}

/** The screen the mechanic actually works on, in the yard: what the order asks
 *  for, the diagnosis, and the moves allowed right now. */
function OrderSheet({ id, onClose }: { id: string; onClose: () => void }) {
  const { data: order, isPending } = useOrder(id)
  const { data: transitions } = useAllowedTransitions(id)
  const changeStatus = useChangeStatus()
  const saveDiagnosis = useSaveDiagnosis(id)
  const [diagnosis, setDiagnosis] = useState<string | null>(null)

  if (isPending || !order) {
    return (
      <div className="fixed inset-0 z-10 bg-surface p-4">
        <p className="text-sm text-ink-soft">Carregando…</p>
      </div>
    )
  }

  const text = diagnosis ?? order.diagnosis ?? ''
  const services = order.items.filter((i) => i.itemType === 'SERVICE')
  const parts = order.items.filter((i) => i.itemType === 'PART')

  return (
    <div className="fixed inset-0 z-10 overflow-y-auto bg-surface">
      <header className="sticky top-0 flex items-center justify-between bg-brand px-4 py-3 text-white">
        <span className="font-semibold">OS-{order.number}</span>
        <button type="button" onClick={onClose} className="text-sm underline underline-offset-2">
          Voltar
        </button>
      </header>

      <div className="space-y-4 p-4">
        <section className="rounded-xl border border-line bg-panel p-4">
          <div className="flex items-center justify-between">
            <p className="font-semibold">{order.vehicleDescription}</p>
            <span className="rounded border border-line px-2 py-0.5 font-mono text-xs">
              {order.vehiclePlate}
            </span>
          </div>
          <div className="mt-3 grid grid-cols-2 gap-3 border-t border-line pt-3 text-sm">
            <div>
              <p className="text-xs text-ink-soft">Cliente</p>
              <p>{order.customerName}</p>
            </div>
            <div>
              <p className="text-xs text-ink-soft">Situação</p>
              <p>{statusLabels[order.status]}</p>
            </div>
          </div>
        </section>

        {order.reportedIssue && (
          <section className="rounded-xl border border-line bg-panel p-4">
            <h2 className="text-sm font-semibold">Relato do cliente</h2>
            <p className="mt-1 text-sm text-ink-soft">{order.reportedIssue}</p>
          </section>
        )}

        <section className="rounded-xl border border-line bg-panel p-4">
          <h2 className="text-sm font-semibold">Peças e Serviços</h2>

          {order.items.length === 0 ? (
            <p className="mt-2 text-sm text-ink-soft">Nenhum item lançado ainda.</p>
          ) : (
            <div className="mt-2 space-y-3 text-sm">
              {parts.length > 0 && (
                <ul className="space-y-1">
                  {parts.map((item) => (
                    <li key={item.id} className="flex justify-between border-b border-line pb-1">
                      <span>{item.description}</span>
                      <span className="text-ink-soft">Qtd: {item.quantity}</span>
                    </li>
                  ))}
                </ul>
              )}
              {services.length > 0 && (
                <ul className="space-y-1">
                  {services.map((item) => (
                    <li key={item.id} className="flex justify-between border-b border-line pb-1">
                      <span>{item.description}</span>
                      <span className="text-ink-soft">Qtd: {item.quantity}</span>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          )}
        </section>

        <section className="rounded-xl border border-line bg-panel p-4">
          <h2 className="text-sm font-semibold">Módulo de Inspeção</h2>

          <label className="mt-3 block">
            <span className="text-xs text-ink-soft">Notas de diagnóstico da OS</span>
            <textarea
              rows={4}
              value={text}
              onChange={(event) => setDiagnosis(event.target.value)}
              placeholder="Descreva avarias adicionais ou peças necessárias…"
              className="mt-1 w-full rounded border border-line px-3 py-2 text-sm"
            />
          </label>

          <button
            type="button"
            disabled={saveDiagnosis.isPending || diagnosis === null}
            onClick={() => saveDiagnosis.mutate(text, { onSuccess: () => setDiagnosis(null) })}
            className="mt-2 rounded border border-line px-3 py-1.5 text-sm disabled:opacity-40"
          >
            {saveDiagnosis.isPending ? 'Salvando…' : 'Salvar diagnóstico'}
          </button>

          {/* O registro fotográfico do protótipo depende do IMediaStorage da
              D-25, que entra na Semana 3 junto com o upload. */}
        </section>

        {changeStatus.isError && (
          <p role="alert" className="rounded-xl border border-line bg-panel p-4 text-sm">
            {(changeStatus.error as Error).message}
          </p>
        )}

        <div className="space-y-2 pb-6">
          {transitions?.length === 0 && (
            <p className="text-sm text-ink-soft">
              Nenhuma ação disponível para você nesta situação.
            </p>
          )}

          {transitions?.map((transition) => (
            <button
              key={transition.toStatus}
              type="button"
              disabled={changeStatus.isPending || transition.requiresNote}
              title={
                transition.requiresNote
                  ? 'Exige justificativa do administrador'
                  : undefined
              }
              onClick={() =>
                changeStatus.mutate(
                  { id: order.id, toStatus: transition.toStatus },
                  { onSuccess: onClose },
                )
              }
              className="w-full rounded-lg bg-brand px-4 py-3 text-sm font-medium text-white disabled:opacity-40"
            >
              {statusLabels[transition.toStatus]}
            </button>
          ))}
        </div>
      </div>
    </div>
  )
}

export function MyQueuePage() {
  const { data: user } = useSession()
  const { data: queue, isPending, isError, error } = useMyQueue()
  const [open, setOpen] = useState<string | null>(null)

  const today = new Date().toLocaleDateString('pt-BR', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
  })

  return (
    <section className="mx-auto max-w-md">
      <header>
        <h1 className="text-2xl font-semibold">Olá, {user?.name.split(' ')[0]}!</h1>
        <p className="text-sm text-ink-soft">Mecânico · {today}</p>
      </header>

      <p className="mt-3 inline-block rounded-full bg-panel px-3 py-1 text-sm">
        {queue?.length ?? 0} {queue?.length === 1 ? 'tarefa na fila' : 'tarefas na fila'}
      </p>

      <h2 className="mt-6 text-sm font-semibold">Minhas tarefas em fila</h2>

      {isError && (
        <p role="alert" className="mt-3 rounded border border-line bg-panel p-4 text-sm">
          {(error as Error).message}
        </p>
      )}

      {isPending ? (
        <p className="mt-4 text-sm text-ink-soft">Carregando…</p>
      ) : queue && queue.length === 0 ? (
        <p className="mt-4 rounded-xl border border-line bg-panel p-6 text-center text-sm text-ink-soft">
          Nenhuma ordem de serviço atribuída a você.
        </p>
      ) : (
        <ul className="mt-3 space-y-3">
          {queue?.map((order) => (
            <QueueCard key={order.id} order={order} onOpen={setOpen} />
          ))}
        </ul>
      )}

      {open && <OrderSheet id={open} onClose={() => setOpen(null)} />}
    </section>
  )
}
