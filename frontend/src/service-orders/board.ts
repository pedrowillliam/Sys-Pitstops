import type { ServiceOrder, ServiceOrderStatus } from './api'

export type BoardColumn = {
  key: string
  label: string
  /** Tailwind class for the stripe on top of the column. */
  accent: string
  holds: (order: ServiceOrder) => boolean
}

/**
 * The column order is written here on purpose. By D-27 the Postgres enum is
 * alphabetical, so ordering by status would show AWAITING_APPROVAL first and
 * REQUESTED last — the board has to carry the workflow order itself.
 *
 * Labels come from the Figma prototype ("KANBAN - ATENDENTE"); the keys are the
 * statuses the API speaks.
 */
export const columns: BoardColumn[] = [
  {
    key: 'REQUESTED',
    label: 'Marcado',
    accent: 'bg-slate-400',
    holds: (o) => o.status === 'REQUESTED',
  },
  {
    key: 'CONFIRMED',
    label: 'No Pátio',
    accent: 'bg-amber-400',
    holds: (o) => o.status === 'CONFIRMED',
  },
  {
    key: 'IN_YARD',
    label: 'Em Análise',
    accent: 'bg-blue-500',
    holds: (o) => o.status === 'IN_YARD',
  },
  // D-35: there is no "Aprovado" column. Approving takes the order straight to
  // IN_PROGRESS when it is awaiting approval, and when it is not, nothing moves
  // — so the fact travels on the card as a badge instead of a column that would
  // almost never hold anything.
  {
    key: 'AWAITING_APPROVAL',
    label: 'Aprovação do Orçamento',
    accent: 'bg-orange-400',
    holds: (o) => o.status === 'AWAITING_APPROVAL',
  },
  {
    key: 'IN_PROGRESS',
    label: 'Em Execução',
    accent: 'bg-emerald-500',
    holds: (o) => o.status === 'IN_PROGRESS',
  },
  {
    key: 'READY',
    label: 'Pronto',
    accent: 'bg-emerald-600',
    holds: (o) => o.status === 'READY',
  },
]

/** Delivered and cancelled orders leave the board: it shows the work in hand. */
export function onTheBoard(order: ServiceOrder): boolean {
  return order.status !== 'DELIVERED' && order.status !== 'CANCELED'
}

export const statusLabels: Record<ServiceOrderStatus, string> = {
  REQUESTED: 'Marcado',
  CONFIRMED: 'No Pátio',
  IN_YARD: 'Em Análise',
  AWAITING_APPROVAL: 'Aprovação do Orçamento',
  IN_PROGRESS: 'Em Execução',
  READY: 'Pronto',
  DELIVERED: 'Entregue',
  CANCELED: 'Cancelada',
}

/** The four tiles above the board, in the order of the prototype. */
export function yardSummary(orders: ServiceOrder[]) {
  const inYard = orders.filter((o) =>
    ['IN_YARD', 'AWAITING_APPROVAL', 'IN_PROGRESS', 'READY'].includes(o.status),
  )

  return [
    { label: 'Veículos no Pátio', value: inYard.length },
    {
      label: 'Em Execução',
      value: orders.filter((o) => o.status === 'IN_PROGRESS').length,
    },
    {
      label: 'Aguardando Aprovação',
      value: orders.filter((o) => o.status === 'AWAITING_APPROVAL').length,
    },
    { label: 'Prontos p/ Retirada', value: orders.filter((o) => o.status === 'READY').length },
  ]
}

export function formatMoney(value: number): string {
  return value.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })
}

export function formatDay(iso: string | null | undefined): string | null {
  if (!iso) {
    return null
  }

  return new Date(iso).toLocaleDateString('pt-BR', { day: '2-digit', month: 'short' })
}
