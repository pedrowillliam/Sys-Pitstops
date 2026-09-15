import { Link } from 'react-router'
import { useSession } from '../auth/session'
import { PageHeader } from '../layout/PageHeader'
import { PlusCircleIcon } from '../layout/icons'
import { useBoard, type ServiceOrder } from '../service-orders/api'
import { columns, formatMoney, onTheBoard, statusLabels, statusTone } from '../service-orders/board'
import { useWorkloads, type MechanicWorkload } from './api'

/** O quadro da tela é um resumo; a lista inteira mora no Kanban, para onde
 *  "Ver todas" aponta. */
const boardRows = 10

function initialsOf(name: string): string {
  const parts = name.trim().split(/\s+/)
  const first = parts.at(0)?.[0] ?? ''
  const last = parts.length > 1 ? (parts.at(-1)?.[0] ?? '') : ''
  return (first + last).toUpperCase()
}

function queueLabel(size: number): string {
  if (size === 0) {
    return 'Fila vazia'
  }

  return `Fila: ${size} ${size === 1 ? 'carro' : 'carros'}`
}

/**
 * Um cartão por mecânico, como no protótipo: foto, nome com o carro em mãos,
 * o próximo, a fila e a etiqueta LIVRE/OCUPADO. A foto são as iniciais (D-47,
 * não há coluna) e a fila não diz "Aprovados" porque, pela D-35, "aprovado"
 * não é uma situação — a OS aprovada já está em execução.
 */
function MechanicCard({ mechanic }: { mechanic: MechanicWorkload }) {
  return (
    <li className="flex flex-col items-center rounded-lg bg-surface p-4 text-center">
      <span
        aria-hidden="true"
        className="flex size-12 items-center justify-center rounded-full bg-brand text-sm font-semibold text-white"
      >
        {initialsOf(mechanic.name)}
      </span>

      <p className="mt-3 text-sm">
        <span className="font-semibold">{mechanic.name}</span>
        {mechanic.current && (
          <>
            {' – '}
            <Link
              to={`/service-orders/${mechanic.current.id}`}
              title={`OS #${mechanic.current.number} · ${mechanic.current.vehiclePlate}`}
              className="hover:underline"
            >
              {mechanic.current.vehicleDescription}
            </Link>
          </>
        )}
      </p>

      <p className="mt-1 text-xs text-ink-soft">
        Próximo:{' '}
        {mechanic.next ? (
          <Link
            to={`/service-orders/${mechanic.next.id}`}
            title={`OS #${mechanic.next.number} · ${mechanic.next.vehiclePlate}`}
            className="hover:underline"
          >
            {mechanic.next.vehicleDescription}
          </Link>
        ) : (
          '—'
        )}
      </p>
      <p className="text-xs text-ink-soft">• {queueLabel(mechanic.queueSize)}</p>

      <span
        className={`mt-3 rounded px-2.5 py-0.5 text-xs font-semibold uppercase ${
          mechanic.isBusy ? 'bg-rose-100 text-rose-700' : 'bg-emerald-100 text-emerald-700'
        }`}
      >
        {mechanic.isBusy ? 'Ocupado' : 'Livre'}
      </span>
    </li>
  )
}

/** Quem cria usuário é o administrador (POST /api/users); para o atendente o
 *  cartão nem aparece, em vez de aparecer e devolver 403. */
function AddCard() {
  return (
    <li>
      <Link
        to="/mechanics/new"
        className="flex h-full min-h-40 flex-col items-center justify-center gap-1 rounded-lg border-2 border-dashed border-line bg-surface/60 p-4 text-ink-soft hover:border-brand hover:text-brand"
      >
        <PlusCircleIcon className="h-6 w-6" />
        <span className="text-xs font-medium">Adicionar</span>
      </Link>
    </li>
  )
}

/** Mais adiantada no fluxo primeiro, como o protótipo lista (execução no topo,
 *  marcadas embaixo); entre iguais, a aberta mais recentemente. */
function stageOf(order: ServiceOrder): number {
  return columns.findIndex((column) => column.holds(order))
}

function boardOrders(orders: ServiceOrder[]): ServiceOrder[] {
  return orders
    .filter(onTheBoard)
    .sort((a, b) => stageOf(b) - stageOf(a) || b.openedAt.localeCompare(a.openedAt))
    .slice(0, boardRows)
}

function OrderRow({ order }: { order: ServiceOrder }) {
  return (
    <tr className="border-t border-line">
      <td className="py-3 pr-3">
        <Link to={`/service-orders/${order.id}`} className="font-semibold text-brand underline">
          OS-{order.number}
        </Link>
      </td>
      <td className="py-3 pr-3">
        <span className="inline-block rounded bg-surface px-2 py-0.5 font-mono text-xs font-semibold">
          {order.vehiclePlate}
        </span>
      </td>
      <td className="py-3 pr-3">{order.vehicleDescription}</td>
      <td className="py-3 pr-3">{order.customerName}</td>
      <td className="py-3 pr-3">
        <span
          className={`inline-block rounded-full px-2.5 py-0.5 text-xs font-medium ${statusTone[order.status].badge}`}
        >
          {statusLabels[order.status]}
        </span>
      </td>
      <td className="py-3 pr-3">{order.mechanicName ?? 'Nenhum'}</td>
      <td className="py-3 text-right font-semibold">{formatMoney(order.total)}</td>
    </tr>
  )
}

export function MechanicsPage() {
  const { data: user } = useSession()
  const workloads = useWorkloads()
  const board = useBoard()

  return (
    <>
      <PageHeader icon="wrench" title="Mecânicos:" />

      <div className="rounded-xl bg-panel p-4 shadow-sm md:p-6">
        {workloads.isPending && <p className="text-sm text-ink-soft">Carregando…</p>}

        {workloads.isError && (
          <p role="alert" className="text-sm">
            {(workloads.error as Error).message}
          </p>
        )}

        {workloads.data && (
          <ul className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
            {workloads.data.map((mechanic) => (
              <MechanicCard key={mechanic.id} mechanic={mechanic} />
            ))}
            {user?.role === 'ADMIN' && <AddCard />}
          </ul>
        )}

        {workloads.data?.length === 0 && user?.role !== 'ADMIN' && (
          <p className="text-sm text-ink-soft">Nenhum mecânico cadastrado.</p>
        )}
      </div>

      <div className="mt-6 rounded-xl bg-panel p-4 shadow-sm md:p-6">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <h2 className="text-lg font-semibold">Quadro de Ordens de Serviço</h2>
          <Link to="/service-orders" className="text-sm font-medium text-brand hover:underline">
            Ver todas ›
          </Link>
        </div>

        {board.isPending && <p className="mt-4 text-sm text-ink-soft">Carregando…</p>}

        {board.isError && (
          <p role="alert" className="mt-4 text-sm">
            {(board.error as Error).message}
          </p>
        )}

        {board.data && (
          <div className="mt-4 overflow-x-auto">
            <table className="w-full min-w-[44rem] text-left text-sm">
              <thead className="text-xs text-ink-soft">
                <tr>
                  <th className="pb-2 pr-3 font-medium">OS#</th>
                  <th className="pb-2 pr-3 font-medium">Placa</th>
                  <th className="pb-2 pr-3 font-medium">Veículo</th>
                  <th className="pb-2 pr-3 font-medium">Cliente</th>
                  <th className="pb-2 pr-3 font-medium">Status</th>
                  <th className="pb-2 pr-3 font-medium">Mecânico</th>
                  <th className="pb-2 text-right font-medium">Valor</th>
                </tr>
              </thead>
              <tbody>
                {boardOrders(board.data).map((order) => (
                  <OrderRow key={order.id} order={order} />
                ))}
              </tbody>
            </table>

            {boardOrders(board.data).length === 0 && (
              <p className="mt-4 text-sm text-ink-soft">Nenhuma ordem de serviço em aberto.</p>
            )}
          </div>
        )}
      </div>
    </>
  )
}
