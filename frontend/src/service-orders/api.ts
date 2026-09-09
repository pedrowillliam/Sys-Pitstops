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
      const { data, error } = await api.GET('/api/users', {
        params: { query: { role: 'MECHANIC' } },
      })

      if (error || !data) {
        throw new Error(messageFrom(error, 'Não foi possível carregar os mecânicos.'))
      }

      return data
    },
  })
}

/** The orders assigned to whoever is signed in, already without the delivered
 *  and cancelled ones — the queue is what is still to do. */
export function useMyQueue() {
  return useQuery({
    queryKey: [...serviceOrdersKey, 'my-queue'],
    queryFn: async () => {
      const { data, error } = await api.GET('/api/service-orders/my-queue')

      if (error || !data) {
        throw new Error(messageFrom(error, 'Não foi possível carregar sua fila.'))
      }

      return data
    },
  })
}

export function useOrder(id: string | undefined) {
  return useQuery({
    queryKey: [...serviceOrdersKey, 'one', id],
    enabled: Boolean(id),
    queryFn: async () => {
      const { data, error } = await api.GET('/api/service-orders/{id}', {
        params: { path: { id: id as string } },
      })

      if (error || !data) {
        throw new Error(messageFrom(error, 'Não foi possível carregar a ordem de serviço.'))
      }

      return data
    },
  })
}

export function useSaveDiagnosis(id: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (diagnosis: string) => {
      const { data, error } = await api.PUT('/api/service-orders/{id}/diagnosis', {
        params: { path: { id } },
        body: { diagnosis: diagnosis.trim() || null },
      })

      if (error || !data) {
        throw new Error(messageFrom(error, 'Não foi possível salvar o diagnóstico.'))
      }

      return data
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: serviceOrdersKey }),
  })
}

export type ServiceOrderDetail = components['schemas']['ServiceOrderDetail']
export type ServiceOrderItem = components['schemas']['ServiceOrderItemResponse']
export type OpenOrderInput = components['schemas']['OpenServiceOrderRequest']
export type UpdateOrderInput = components['schemas']['UpdateServiceOrderRequest']
export type ItemInput = components['schemas']['ServiceOrderItemRequest']

export function useOpenOrder() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (input: OpenOrderInput) => {
      const { data, error } = await api.POST('/api/service-orders', { body: input })

      if (error || !data) {
        throw new Error(messageFrom(error, 'Não foi possível abrir a ordem de serviço.'))
      }

      return data
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: serviceOrdersKey }),
  })
}

export function useUpdateOrder(id: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (input: UpdateOrderInput) => {
      const { data, error } = await api.PUT('/api/service-orders/{id}', {
        params: { path: { id } },
        body: input,
      })

      if (error || !data) {
        throw new Error(messageFrom(error, 'Não foi possível salvar a ordem de serviço.'))
      }

      return data
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: serviceOrdersKey }),
  })
}

/** O preço vai congelado no lançamento (D-07): o que é enviado aqui é o que a
 *  ordem mostra para sempre, mesmo que a peça mude de preço depois. */
export function useSaveItem(orderId: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async ({ itemId, ...body }: ItemInput & { itemId?: string }) => {
      const { data, error } = itemId
        ? await api.PUT('/api/service-orders/{id}/items/{itemId}', {
            params: { path: { id: orderId, itemId } },
            body,
          })
        : await api.POST('/api/service-orders/{id}/items', {
            params: { path: { id: orderId } },
            body,
          })

      if (error || !data) {
        throw new Error(messageFrom(error, 'Não foi possível salvar o item.'))
      }

      return data
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: serviceOrdersKey }),
  })
}

export function useRemoveItem(orderId: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (itemId: string) => {
      const { error } = await api.DELETE('/api/service-orders/{id}/items/{itemId}', {
        params: { path: { id: orderId, itemId } },
      })

      if (error) {
        throw new Error(messageFrom(error, 'Não foi possível remover o item.'))
      }
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: serviceOrdersKey }),
  })
}
