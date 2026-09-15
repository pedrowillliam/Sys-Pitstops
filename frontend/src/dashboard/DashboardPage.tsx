import { Link } from 'react-router'
import { PageHeader } from '../layout/PageHeader'
import { BoxIcon, ClipboardIcon, CurrencyIcon, UsersIcon, WrenchIcon } from '../layout/icons'
import { statusLabels, statusTone } from '../service-orders/board'
import type { ServiceOrderStatus } from '../service-orders/api'
import {
  formatCompact,
  formatDay,
  formatHours,
  formatMoney,
  monthLabel,
  useDashboard,
  type Dashboard,
  type MechanicProductivity,
  type MonthlyMetric,
  type RecentOrder,
} from './api'

/** As quatro fichas do topo do protótipo. "Veículos no Pátio" junta tudo que
 *  está fisicamente na oficina, do recebimento à retirada. */
function yardTiles(byStatus: Dashboard['byStatus']) {
  const count = (status: ServiceOrderStatus) =>
    byStatus.find((entry) => entry.status === status)?.count ?? 0

  const inYard =
    count('IN_YARD') + count('AWAITING_APPROVAL') + count('IN_PROGRESS') + count('READY')

  return [
    { label: 'Veículos no Pátio', value: inYard },
    { label: 'Em Execução', value: count('IN_PROGRESS') },
    { label: 'Aguardando Aprovação', value: count('AWAITING_APPROVAL') },
    { label: 'Prontos p/ Retirada', value: count('READY') },
  ]
}

function Change({ metric }: { metric: MonthlyMetric }) {
  // Sem mês anterior não há comparação: o servidor devolve nulo em vez de
  // inventar um "+100%" que o dado não sustenta.
  if (metric.changePercent === null || metric.changePercent === undefined) {
    return <p className="mt-1 text-xs text-ink-soft">sem mês anterior para comparar</p>
  }

  const up = metric.changePercent >= 0

  return (
    <p className="mt-1 text-xs text-ink-soft">
      <span className={`font-semibold ${up ? 'text-emerald-600' : 'text-rose-600'}`}>
        {up ? '+' : ''}
        {metric.changePercent.toLocaleString('pt-BR', { maximumFractionDigits: 1 })}%
      </span>{' '}
      em relação ao mês anterior
    </p>
  )
}

function KpiCard({
  label,
  value,
  metric,
  icon: Icon,
}: {
  label: string
  value: string
  metric: MonthlyMetric
  icon: typeof CurrencyIcon
}) {
  return (
    <div className="rounded-xl border border-line bg-panel p-5">
      <div className="flex items-start justify-between gap-3">
        <p className="text-sm text-ink-soft">{label}</p>
        <span aria-hidden="true" className="text-ink-soft">
          <Icon className="h-5 w-5" />
        </span>
      </div>

      <p className="mt-2 text-2xl font-bold">{value}</p>
      <Change metric={metric} />
    </div>
  )
}

/** Barras em CSS puro. Uma biblioteca de gráfico entraria no bundle inteira
 *  para desenhar seis retângulos, e o PWA precisa abrir rápido no pátio. */
function RevenueChart({ history }: { history: Dashboard['revenueHistory'] }) {
  const peak = Math.max(...history.map((month) => month.total), 1)

  return (
    <div className="rounded-xl border border-line bg-panel p-5">
      <h2 className="text-base font-semibold">Faturamento Mensal</h2>
      <p className="text-sm text-ink-soft">Evolução dos últimos 6 meses</p>

      <div className="mt-6 flex gap-3">
        {history.map((month, index) => {
          const last = index === history.length - 1

          return (
            <div key={`${month.year}-${month.month}`} className="flex flex-1 flex-col items-center">
              <span className="mb-1 text-xs text-ink-soft">{formatCompact(month.total)}</span>

              {/* O trilho precisa de altura própria: a barra é uma porcentagem
                  dele, e porcentagem contra pai de altura automática não
                  resolve — as barras somem e sobram só os rótulos. */}
              <div className="flex h-44 w-full items-end">
                <div
                  className={`w-full rounded-t ${last ? 'bg-brand' : 'bg-sky-300'}`}
                  style={{ height: `${Math.max((month.total / peak) * 100, 2)}%` }}
                  // O mês corrente ainda está correndo: a barra menor no fim é
                  // o esperado, não uma queda.
                  title={last ? 'Mês em andamento' : undefined}
                />
              </div>

              <span className="mt-2 text-xs text-ink-soft">{monthLabel(month)}</span>
            </div>
          )
        })}
      </div>
    </div>
  )
}

function MechanicRow({ mechanic, place }: { mechanic: MechanicProductivity; place: number }) {
  return (
    <li className="flex items-center gap-3 py-2.5">
      <span
        aria-hidden="true"
        className="grid size-6 shrink-0 place-items-center rounded-full bg-surface text-xs font-semibold text-ink-soft"
      >
        {place}
      </span>

      <span className="min-w-0 flex-1 truncate text-sm font-medium">{mechanic.name}</span>

      <span className="shrink-0 text-right text-sm">
        <span className="font-semibold">{mechanic.closedOrders}</span>
        <span className="text-ink-soft"> OS</span>
        <span className="block text-xs text-ink-soft">{formatMoney(mechanic.revenue)}</span>
      </span>
    </li>
  )
}

function RecentRow({ order }: { order: RecentOrder }) {
  return (
    <tr className="border-t border-line">
      <td className="py-2.5 pr-3">
        <Link to={`/service-orders/${order.id}`} className="font-medium text-brand underline">
          #{order.number}
        </Link>
      </td>
      <td className="py-2.5 pr-3">{order.customerName}</td>
      <td className="py-2.5 pr-3">
        {order.vehicleDescription}
        <span className="block font-mono text-xs text-ink-soft">{order.vehiclePlate}</span>
      </td>
      <td className="py-2.5 pr-3 text-ink-soft">{formatDay(order.openedAt)}</td>
      <td className="py-2.5 pr-3">{order.mechanicName ?? '—'}</td>
      <td className="py-2.5 pr-3 font-medium">{formatMoney(order.total)}</td>
      <td className="py-2.5">
        <span
          className={`inline-block rounded-full px-2.5 py-0.5 text-xs font-medium ${statusTone[order.status].badge}`}
        >
          {statusLabels[order.status]}
        </span>
      </td>
    </tr>
  )
}

export function DashboardPage() {
  const { data, isPending, isError, error } = useDashboard()

  if (isPending) {
    return (
      <>
        <PageHeader icon="dashboard" title="Status do Pátio" />
        <p className="text-sm text-ink-soft">Carregando…</p>
      </>
    )
  }

  if (isError || !data) {
    return (
      <>
        <PageHeader icon="dashboard" title="Status do Pátio" />
        <p role="alert" className="text-sm">
          {(error as Error)?.message ?? 'Não foi possível carregar o painel.'}
        </p>
      </>
    )
  }

  return (
    <>
      <PageHeader icon="dashboard" title="Status do Pátio" />

      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        {yardTiles(data.byStatus).map((tile) => (
          <div key={tile.label} className="rounded-xl border border-line bg-panel p-5">
            <p className="text-sm text-ink-soft">{tile.label}</p>
            <p className="mt-1 text-3xl font-bold">{tile.value}</p>
          </div>
        ))}
      </div>

      <div className="mt-6 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <KpiCard
          label="Faturamento do Mês"
          value={formatMoney(data.revenue.current)}
          metric={data.revenue}
          icon={CurrencyIcon}
        />
        <KpiCard
          label="OS Abertas no Mês"
          value={String(data.openedOrders.current)}
          metric={data.openedOrders}
          icon={ClipboardIcon}
        />
        <KpiCard
          label="Clientes Atendidos"
          value={String(data.customersServed.current)}
          metric={data.customersServed}
          icon={UsersIcon}
        />
        <KpiCard
          label="Ticket Médio"
          value={formatMoney(data.averageTicket.current)}
          metric={data.averageTicket}
          icon={CurrencyIcon}
        />
      </div>

      <div className="mt-6 grid gap-4 lg:grid-cols-[3fr_2fr]">
        <RevenueChart history={data.revenueHistory} />

        <div className="rounded-xl border border-line bg-panel p-5">
          <h2 className="text-base font-semibold">Produtividade dos Mecânicos</h2>
          {/* D-47: só OS concluídas. A nota de qualidade e a especialidade que o
              protótipo desenha não têm origem no modelo. */}
          <p className="text-sm text-ink-soft">Ranking por OS concluídas no mês</p>

          {data.mechanics.length === 0 ? (
            <p className="mt-4 text-sm text-ink-soft">Nenhum mecânico cadastrado.</p>
          ) : (
            <ul className="mt-3 divide-y divide-line">
              {data.mechanics.map((mechanic, index) => (
                <MechanicRow key={mechanic.id} mechanic={mechanic} place={index + 1} />
              ))}
            </ul>
          )}
        </div>
      </div>

      {/* Os dois KPIs da §6 que o protótipo não desenha, mas que a tela existe
          para responder — o tempo de execução só é calculável porque o
          histórico de transições existe. */}
      <div className="mt-6 grid gap-4 sm:grid-cols-2">
        <div className="flex items-center gap-4 rounded-xl border border-line bg-panel p-5">
          <span aria-hidden="true" className="text-ink-soft">
            <WrenchIcon className="h-6 w-6" />
          </span>
          <div>
            <p className="text-sm text-ink-soft">Tempo médio de execução</p>
            <p className="mt-0.5 text-xl font-bold">
              {data.averageExecutionHours === null || data.averageExecutionHours === undefined
                ? 'sem dados'
                : formatHours(data.averageExecutionHours)}
            </p>
          </div>
        </div>

        <Link
          to="/inventory"
          className="flex items-center gap-4 rounded-xl border border-line bg-panel p-5 hover:bg-surface"
        >
          <span aria-hidden="true" className="text-ink-soft">
            <BoxIcon className="h-6 w-6" />
          </span>
          <div>
            <p className="text-sm text-ink-soft">Peças abaixo do mínimo</p>
            <p className="mt-0.5 text-xl font-bold">{data.partsBelowMinimum}</p>
          </div>
        </Link>
      </div>

      {data.topServices.length > 0 && (
        <div className="mt-6 rounded-xl border border-line bg-panel p-5">
          <h2 className="text-base font-semibold">Serviços mais executados</h2>
          <p className="text-sm text-ink-soft">No mês, pelas OS concluídas</p>

          <ul className="mt-3 divide-y divide-line">
            {data.topServices.map((service) => (
              <li key={service.description} className="flex items-center gap-3 py-2.5 text-sm">
                <span className="min-w-0 flex-1 truncate">{service.description}</span>
                <span className="shrink-0 text-ink-soft">{service.times}×</span>
                <span className="shrink-0 font-medium">{formatMoney(service.revenue)}</span>
              </li>
            ))}
          </ul>
        </div>
      )}

      <div className="mt-6 rounded-xl border border-line bg-panel p-5">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <h2 className="text-base font-semibold">Ordens de Serviço Recentes</h2>
            <p className="text-sm text-ink-soft">Últimas atualizações do pátio</p>
          </div>
          <Link
            to="/service-orders"
            className="rounded-lg border border-line px-3 py-1.5 text-sm font-medium hover:bg-surface"
          >
            Ver todas as OS
          </Link>
        </div>

        <div className="mt-4 overflow-x-auto">
          <table className="w-full min-w-[44rem] text-left text-sm">
            <thead className="text-xs uppercase text-ink-soft">
              <tr>
                <th className="pb-2 pr-3 font-medium">OS</th>
                <th className="pb-2 pr-3 font-medium">Cliente</th>
                <th className="pb-2 pr-3 font-medium">Veículo</th>
                <th className="pb-2 pr-3 font-medium">Data entrada</th>
                <th className="pb-2 pr-3 font-medium">Mecânico</th>
                <th className="pb-2 pr-3 font-medium">Valor total</th>
                <th className="pb-2 font-medium">Status</th>
              </tr>
            </thead>
            <tbody>
              {data.recent.map((order) => (
                <RecentRow key={order.id} order={order} />
              ))}
            </tbody>
          </table>
        </div>

        {data.recent.length === 0 && (
          <p className="mt-4 text-sm text-ink-soft">Nenhuma ordem de serviço ainda.</p>
        )}
      </div>
    </>
  )
}
