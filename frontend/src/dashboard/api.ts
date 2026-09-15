import { useQuery } from '@tanstack/react-query'
import { api } from '../api/client'
import type { components } from '../api/schema'

export type Dashboard = components['schemas']['DashboardResponse']
export type MonthlyMetric = components['schemas']['MonthlyMetric']
export type MonthlyRevenue = components['schemas']['MonthlyRevenue']
export type MechanicProductivity = components['schemas']['MechanicProductivity']
export type RecentOrder = components['schemas']['RecentOrder']

function messageFrom(error: unknown, fallback: string): string {
  const problem = error as { title?: string; detail?: string } | undefined
  return problem?.detail ?? problem?.title ?? fallback
}

export function useDashboard() {
  return useQuery({
    queryKey: ['dashboard'],
    queryFn: async () => {
      const { data, error } = await api.GET('/api/dashboard')

      if (error || !data) {
        throw new Error(messageFrom(error, 'Não foi possível carregar o painel.'))
      }

      return data
    },
  })
}

const money = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })

export function formatMoney(value: number): string {
  return money.format(value)
}

/** "R$ 15,8 mil" no eixo do gráfico: o valor cheio não cabe, e o que a barra
 *  precisa comunicar é a ordem de grandeza. */
export function formatCompact(value: number): string {
  if (value >= 1000) {
    return `R$ ${(value / 1000).toLocaleString('pt-BR', { maximumFractionDigits: 1 })} mil`
  }

  return money.format(value)
}

const months = [
  'Jan', 'Fev', 'Mar', 'Abr', 'Mai', 'Jun',
  'Jul', 'Ago', 'Set', 'Out', 'Nov', 'Dez',
]

export function monthLabel(entry: MonthlyRevenue): string {
  return months[entry.month - 1] ?? ''
}

export function formatDay(iso: string): string {
  return new Date(iso).toLocaleDateString('pt-BR')
}

/** Horas viram "1 d 6 h" quando passam de um dia: o tempo médio de execução de
 *  uma oficina raramente cabe bem em horas soltas. */
export function formatHours(hours: number): string {
  if (hours < 24) {
    return `${hours.toLocaleString('pt-BR', { maximumFractionDigits: 1 })} h`
  }

  const days = Math.floor(hours / 24)
  const rest = Math.round(hours % 24)

  return rest === 0 ? `${days} d` : `${days} d ${rest} h`
}
