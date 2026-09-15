import type { ServiceOrder, ServiceOrderStatus } from './api'

/**
 * A cor de cada situação, em um lugar só.
 *
 * Havia duas escalas antes desta, e elas discordavam: no quadro a execução era
 * verde, na fila do mecânico era azul. Quem olha as duas telas aprende a cor
 * errada.
 *
 * A escala segue o fluxo: cinza enquanto nada aconteceu, azul quando o carro
 * chega, âmbar durante a análise, laranja enquanto a resposta depende do
 * cliente, azul forte na execução, e verde no fim. As classes são escritas
 * inteiras porque o Tailwind não enxerga nome de classe montado em tempo de
 * execução.
 */
export const statusTone: Record<
  ServiceOrderStatus,
  { badge: string; dot: string; accent: string }
> = {
  REQUESTED: {
    badge: 'bg-slate-100 text-slate-700',
    dot: 'bg-slate-400',
    accent: 'bg-slate-400',
  },
  CONFIRMED: {
    badge: 'bg-sky-100 text-sky-700',
    dot: 'bg-sky-500',
    accent: 'bg-sky-500',
  },
  IN_YARD: {
    badge: 'bg-amber-100 text-amber-700',
    dot: 'bg-amber-500',
    accent: 'bg-amber-500',
  },
  AWAITING_APPROVAL: {
    badge: 'bg-orange-100 text-orange-700',
    dot: 'bg-orange-500',
    accent: 'bg-orange-400',
  },
  IN_PROGRESS: {
    badge: 'bg-blue-100 text-blue-700',
    dot: 'bg-blue-500',
    accent: 'bg-blue-600',
  },
  READY: {
    badge: 'bg-teal-100 text-teal-700',
    dot: 'bg-teal-500',
    accent: 'bg-teal-500',
  },
  DELIVERED: {
    badge: 'bg-emerald-100 text-emerald-700',
    dot: 'bg-emerald-500',
    accent: 'bg-emerald-600',
  },
  CANCELED: {
    badge: 'bg-rose-100 text-rose-700',
    dot: 'bg-rose-500',
    accent: 'bg-rose-500',
  },
}

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
    accent: statusTone.REQUESTED.accent,
    holds: (o) => o.status === 'REQUESTED',
  },
  {
    key: 'CONFIRMED',
    label: 'No Pátio',
    accent: statusTone.CONFIRMED.accent,
    holds: (o) => o.status === 'CONFIRMED',
  },
  {
    key: 'IN_YARD',
    label: 'Em Análise',
    accent: statusTone.IN_YARD.accent,
    holds: (o) => o.status === 'IN_YARD',
  },
  // D-35: there is no "Aprovado" column. Approving takes the order straight to
  // IN_PROGRESS when it is awaiting approval, and when it is not, nothing moves
  // — so the fact travels on the card as a badge instead of a column that would
  // almost never hold anything.
  {
    key: 'AWAITING_APPROVAL',
    label: 'Aprovação do Orçamento',
    accent: statusTone.AWAITING_APPROVAL.accent,
    holds: (o) => o.status === 'AWAITING_APPROVAL',
  },
  {
    key: 'IN_PROGRESS',
    label: 'Em Execução',
    accent: statusTone.IN_PROGRESS.accent,
    holds: (o) => o.status === 'IN_PROGRESS',
  },
  {
    key: 'READY',
    label: 'Pronto',
    accent: statusTone.READY.accent,
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

/** Espelha ServiceOrderWorkflow.AllowsItemChanges: entregue, cancelada e pronta
 *  não recebem mais lançamento. O servidor recusa de qualquer jeito; isto só
 *  evita oferecer um botão que vai dar erro. */
export function allowsItemChanges(status: ServiceOrderStatus): boolean {
  return status !== 'DELIVERED' && status !== 'CANCELED' && status !== 'READY'
}

/** Espelha a D-38: sem responsável a OS não entra em análise. O quadro deduz
 *  isso sozinho só para explicar por que o botão não está lá — quem recusa de
 *  fato é o servidor, que é onde a regra mora. */
export function needsMechanicToAdvance(order: ServiceOrder): boolean {
  return order.status === 'CONFIRMED' && !order.mechanicId
}
