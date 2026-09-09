import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../api/client'
import { serviceOrdersKey } from '../service-orders/api'
import type { components } from '../api/schema'

export type Quote = components['schemas']['QuoteListItem']
export type QuoteStatus = components['schemas']['QuoteStatus']

export const quotesKey = ['quotes'] as const
export const pageSize = 20

function messageFrom(error: unknown, fallback: string): string {
  const problem = error as { title?: string; detail?: string } | undefined
  return problem?.detail ?? problem?.title ?? fallback
}

export function useQuotes(status: QuoteStatus | 'ALL', page: number) {
  return useQuery({
    queryKey: [...quotesKey, status, page],
    queryFn: async () => {
      const { data, error } = await api.GET('/api/quotes', {
        params: {
          query: { status: status === 'ALL' ? undefined : status, page, pageSize },
        },
      })

      if (error || !data) {
        throw new Error(messageFrom(error, 'Não foi possível carregar os orçamentos.'))
      }

      return data
    },
    placeholderData: (previous) => previous,
  })
}

/** Enviar de novo não corrige o anterior: o orçamento é uma fotografia dos
 *  itens (D-10), então a API expira o que estava de pé e cria outro. O quadro
 *  também muda, porque a etiqueta da D-35 vem da OS. */
export function useSendQuote() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (orderId: string) => {
      const { data, error } = await api.POST('/api/service-orders/{orderId}/quotes', {
        params: { path: { orderId } },
      })

      if (error || !data) {
        throw new Error(messageFrom(error, 'Não foi possível gerar o orçamento.'))
      }

      return data
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: quotesKey })
      await queryClient.invalidateQueries({ queryKey: serviceOrdersKey })
    },
  })
}

export type OrderQuote = components['schemas']['QuoteResponse']

/** Os orçamentos de uma OS, do mais novo para o mais antigo. O painel do quadro
 *  usa para mostrar se já existe um link de pé antes de gerar outro. */
export function useOrderQuotes(orderId: string) {
  return useQuery({
    queryKey: [...quotesKey, 'order', orderId],
    queryFn: async () => {
      const { data, error } = await api.GET('/api/service-orders/{orderId}/quotes', {
        params: { path: { orderId } },
      })

      if (error || !data) {
        throw new Error(messageFrom(error, 'Não foi possível carregar os orçamentos da OS.'))
      }

      return data
    },
  })
}
