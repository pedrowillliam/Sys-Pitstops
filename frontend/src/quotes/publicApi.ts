import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../api/client'
import type { components } from '../api/schema'

export type PublicQuote = components['schemas']['PublicQuoteResponse']
export type QuoteItem = components['schemas']['QuoteItemSnapshot']

const publicQuoteKey = (token: string) => ['public-quote', token] as const

/** The API answers the same 404 for an unknown and an expired link, so the
 *  page says the same thing for both — telling them apart would confirm to
 *  whoever is guessing that a token existed (D-21). */
function messageFrom(error: unknown, fallback: string): string {
  const problem = error as { title?: string; detail?: string } | undefined
  return problem?.title ?? problem?.detail ?? fallback
}

export function usePublicQuote(token: string) {
  return useQuery({
    queryKey: publicQuoteKey(token),
    retry: false,
    queryFn: async () => {
      const { data, error, response } = await api.GET('/api/public/quotes/{token}', {
        params: { path: { token } },
      })

      if (response.status === 404) {
        return null
      }

      if (error || !data) {
        throw new Error(messageFrom(error, 'Não foi possível carregar o orçamento.'))
      }

      return data
    },
  })
}

export function useAnswerQuote(token: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (answer: { approve: boolean; reason?: string }) => {
      const { data, error } = answer.approve
        ? await api.POST('/api/public/quotes/{token}/approve', {
            params: { path: { token } },
          })
        : await api.POST('/api/public/quotes/{token}/reject', {
            params: { path: { token } },
            body: { reason: answer.reason ?? null },
          })

      if (error || !data) {
        throw new Error(messageFrom(error, 'Não foi possível registrar sua resposta.'))
      }

      return data
    },
    onSuccess: (quote) => queryClient.setQueryData(publicQuoteKey(token), quote),
  })
}

export function money(value: number): string {
  return value.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })
}
