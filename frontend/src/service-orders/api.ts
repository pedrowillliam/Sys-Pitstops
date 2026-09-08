import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../api/client'
import type { components } from '../api/schema'

export type ServiceOrder = components['schemas']['ServiceOrderSummary']
export type ServiceOrderStatus = components['schemas']['ServiceOrderStatus']
export type AllowedTransition = components['schemas']['AllowedTransition']

export const serviceOrdersKey = ['service-orders'] as const

/** The board reads everything at once and groups in memory, so the column
 *  counts always agree with the cards. A workshop does not hold hundreds of
 *  open orders; when it does, this becomes one query per column. */
const boardPageSize = 100

function messageFrom(error: unknown, fallback: string): string {
  const problem = error as { title?: string; detail?: string } | undefined
  return problem?.detail ?? problem?.title ?? fallback
}

export function useBoard(mechanicId?: string) {
  return useQuery({
    queryKey: [...serviceOrdersKey, 'board', mechanicId ?? 'all'],
    queryFn: async () => {
      const { data, error } = await api.GET('/api/service-orders', {
        params: { query: { mechanicId, pageSize: boardPageSize } },
      })

      if (error || !data) {
        throw new Error(messageFrom(error, 'Não foi possível carregar o quadro.'))
      }

      return data.items
    },
    placeholderData: (previous) => previous,
  })
}

/** What this user may do with this order right now. The rules stay on the
 *  server (D-13); the board only draws the buttons it is told about. */
export function useAllowedTransitions(id: string | undefined) {
  return useQuery({
    queryKey: [...serviceOrdersKey, 'transitions', id],
    enabled: Boolean(id),
    queryFn: async () => {
      const { data, error } = await api.GET('/api/service-orders/{id}/allowed-transitions', {
        params: { path: { id: id as string } },
      })

      if (error || !data) {
        throw new Error(messageFrom(error, 'Não foi possível carregar as ações.'))
      }

      return data
    },
  })
}

export function useChangeStatus() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (move: {
      id: string
      toStatus: ServiceOrderStatus
      note?: string
      waiveApproval?: boolean
    }) => {
      const { data, error } = await api.POST('/api/service-orders/{id}/status', {
        params: { path: { id: move.id } },
        body: {
          toStatus: move.toStatus,
          note: move.note ?? null,
          waiveApproval: move.waiveApproval ?? false,
        },
      })

      if (error || !data) {
        // A refused transition answers 409 carrying the workflow's own words.
        throw new Error(messageFrom(error, 'Não foi possível mover a ordem de serviço.'))
      }

      return data
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: serviceOrdersKey }),
  })
}

export function useMechanics() {
  return useQuery({
    queryKey: ['mechanics'],
    queryFn: async () => {
      // The users endpoint does not exist yet; the filter reads the mechanics
      // already assigned to orders instead of inventing a route.
      const { data } = await api.GET('/api/service-orders', {
        params: { query: { pageSize: boardPageSize } },
      })

      const seen = new Map<string, string>()
      for (const order of data?.items ?? []) {
        if (order.mechanicId && order.mechanicName) {
          seen.set(order.mechanicId, order.mechanicName)
        }
      }

      return [...seen].map(([id, name]) => ({ id, name }))
    },
  })
}
