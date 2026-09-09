import { useState } from 'react'
import { Link, useParams } from 'react-router'
import { QuoteCard } from '../quotes/QuoteCard'
import { allowsItemChanges, formatDay, statusLabels } from './board'
import { ItemsCard } from './ItemsCard'
import { useMechanics, useOrder, useUpdateOrder, type ServiceOrderDetail } from './api'

function Facts({ order }: { order: ServiceOrderDetail }) {
  const rows: [string, string][] = [
    ['Cliente', order.customerName],
    ['Telefone', order.customerPhone],
    ['Veículo', `${order.vehicleDescription} · ${order.vehiclePlate}`],
    ['Quilometragem', order.mileage ? `${order.mileage} km` : '—'],
    ['Aberta em', formatDay(order.openedAt) ?? '—'],
    ['Previsão', formatDay(order.scheduledAt) ?? '—'],
  ]

  return (
    <dl className="grid gap-x-6 gap-y-2 sm:grid-cols-2">
      {rows.map(([label, value]) => (
        <div key={label} className="flex justify-between gap-3 text-sm">
          <dt className="text-ink-soft">{label}</dt>
          <dd className="text-right font-medium">{value}</dd>
        </div>
      ))}
    </dl>
  )
}

/** O responsável é o que destrava "Em Análise" (D-38), então ele fica editável
 *  aqui em vez de escondido numa tela de edição separada. */
function MechanicPicker({ order }: { order: ServiceOrderDetail }) {
  const { data: mechanics } = useMechanics()
  const update = useUpdateOrder(order.id)
  const [value, setValue] = useState(order.mechanicId ?? '')

  const changed = (order.mechanicId ?? '') !== value

  function save() {
    update.mutate({
      mechanicId: value || null,
      mileage: order.mileage,
      reportedIssue: order.reportedIssue,
      discountAmount: order.discountAmount,
      scheduledAt: order.scheduledAt,
    })
  }

  return (
    <div className="mt-4 border-t border-line pt-3">
      <label className="block">
        <span className="text-sm font-medium">Mecânico responsável</span>
        <div className="mt-1 flex gap-2">
          <select
            value={value}
            onChange={(event) => setValue(event.target.value)}
            className="w-full rounded border border-line bg-panel px-3 py-2 text-sm"
          >
            <option value="">Sem responsável</option>
            {mechanics?.map((mechanic) => (
              <option key={mechanic.id} value={mechanic.id}>
                {mechanic.name}
              </option>
            ))}
          </select>
          <button
            type="button"
            disabled={!changed || update.isPending}
            onClick={save}
            className="shrink-0 rounded bg-brand px-3 py-2 text-sm font-medium text-white disabled:opacity-40"
          >
            {update.isPending ? 'Salvando…' : 'Salvar'}
          </button>
        </div>
      </label>

      {!order.mechanicId && order.status === 'CONFIRMED' && (
        <p className="mt-2 rounded bg-amber-50 px-3 py-2 text-xs text-amber-900">
          Defina o responsável para poder mover a OS para Em Análise.
        </p>
      )}

      {update.isError && (
        <p role="alert" className="mt-2 text-sm">
          {(update.error as Error).message}
        </p>
      )}
    </div>
  )
}

function History({ order }: { order: ServiceOrderDetail }) {
  return (
    <div className="rounded-lg border border-line bg-panel p-4">
      <h2 className="text-sm font-semibold">Histórico</h2>
      <ol className="mt-3 space-y-2 text-sm">
        {order.history.map((entry) => (
          <li key={entry.id} className="flex flex-wrap gap-x-2 text-ink-soft">
            <span className="font-medium text-ink">{statusLabels[entry.toStatus]}</span>
            <span>· {entry.changedByName}</span>
            <span>· {formatDay(entry.changedAt)}</span>
            {entry.note && <span className="w-full text-xs">{entry.note}</span>}
          </li>
        ))}
      </ol>
    </div>
  )
}

export function OrderDetailPage() {
  const { id } = useParams()
  const { data: order, isPending, isError, error } = useOrder(id)

  if (isPending) {
    return <p className="text-sm text-ink-soft">Carregando…</p>
  }

  if (isError || !order) {
    return (
      <p role="alert" className="text-sm">
        {(error as Error)?.message ?? 'Ordem de serviço não encontrada.'}
      </p>
    )
  }

  return (
    <section className="space-y-4">
      <header className="flex flex-wrap items-center justify-between gap-3 rounded bg-brand px-4 py-3 text-white">
        <div>
          <h1 className="text-lg font-semibold">
            OS-{order.number} · {order.vehiclePlate}
          </h1>
          <p className="text-sm opacity-80">{statusLabels[order.status]}</p>
        </div>
        <Link to="/service-orders" className="rounded border border-white/40 px-3 py-1 text-sm">
          Voltar ao quadro
        </Link>
      </header>

      <div className="grid gap-4 lg:grid-cols-2">
        <div className="rounded-lg border border-line bg-panel p-4">
          <h2 className="text-sm font-semibold">Dados da OS</h2>
          <div className="mt-3">
            <Facts order={order} />
          </div>

          {order.reportedIssue && (
            <div className="mt-4 border-t border-line pt-3">
              <h3 className="text-sm font-medium">Problema relatado</h3>
              <p className="mt-1 text-sm text-ink-soft">{order.reportedIssue}</p>
            </div>
          )}

          {order.diagnosis && (
            <div className="mt-4 border-t border-line pt-3">
              <h3 className="text-sm font-medium">Laudo do mecânico</h3>
              <p className="mt-1 text-sm text-ink-soft">{order.diagnosis}</p>
            </div>
          )}

          <MechanicPicker order={order} />
        </div>

        <ItemsCard
          orderId={order.id}
          items={order.items}
          itemsTotal={order.itemsTotal}
          discount={order.discountAmount}
          total={order.total}
          editable={allowsItemChanges(order.status)}
        />

        <QuoteCard order={order} />
        <History order={order} />
      </div>
    </section>
  )
}
