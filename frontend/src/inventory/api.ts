import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../api/client'
import type { components } from '../api/schema'

export type Part = components['schemas']['PartResponse']
export type PartInput = components['schemas']['PartRequest']
export type StockMovement = components['schemas']['StockMovementResponse']
export type StockMovementInput = components['schemas']['StockMovementRequest']
export type MovementType = components['schemas']['MovementType']

export type PartFilters = {
  search: string
  lowStock: boolean
  includeInactive: boolean
  page: number
}

export const partsKey = ['parts'] as const

export const pageSize = 20

function messageFrom(error: unknown, fallback: string): string {
  const problem = error as { title?: string; detail?: string } | undefined
  return problem?.detail ?? problem?.title ?? fallback
}

export function useParts(filters: PartFilters) {
  return useQuery({
    queryKey: [...partsKey, 'list', filters],
    queryFn: async () => {
      const { data, error } = await api.GET('/api/parts', {
        params: {
          query: {
            search: filters.search || undefined,
            lowStock: filters.lowStock,
            includeInactive: filters.includeInactive,
            page: filters.page,
            pageSize,
          },
        },
      })

      if (error || !data) {
        throw new Error(messageFrom(error, 'Não foi possível carregar o estoque.'))
      }

      return data
    },
    placeholderData: (previous) => previous,
  })
}

export function usePart(id: string | undefined) {
  return useQuery({
    queryKey: [...partsKey, 'one', id],
    enabled: Boolean(id),
    queryFn: async () => {
      const { data, error } = await api.GET('/api/parts/{id}', {
        params: { path: { id: id as string } },
      })

      if (error || !data) {
        throw new Error(messageFrom(error, 'Não foi possível carregar a peça.'))
      }

      return data
    },
  })
}

export function usePartMovements(id: string | undefined) {
  return useQuery({
    queryKey: [...partsKey, 'movements', id],
    enabled: Boolean(id),
    queryFn: async () => {
      const { data, error } = await api.GET('/api/parts/{id}/movements', {
        params: { path: { id: id as string }, query: { pageSize } },
      })

      if (error || !data) {
        throw new Error(messageFrom(error, 'Não foi possível carregar a movimentação.'))
      }

      return data
    },
  })
}

export function useSavePart(id?: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (input: PartInput) => {
      const { data, error } = id
        ? await api.PUT('/api/parts/{id}', { params: { path: { id } }, body: input })
        : await api.POST('/api/parts', { body: input })

      if (error || !data) {
        throw new Error(messageFrom(error, 'Não foi possível salvar a peça.'))
      }

      return data
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: partsKey }),
  })
}

/**
 * The only way the balance moves by hand. The backend writes the movement and
 * the new balance in one transaction (data-model.md, section 4), so there is
 * nothing to reconcile here — the response already carries the new balance.
 */
export function useMoveStock(id: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (input: StockMovementInput) => {
      const { data, error } = await api.POST('/api/parts/{id}/movements', {
        params: { path: { id } },
        body: input,
      })

      if (error || !data) {
        throw new Error(messageFrom(error, 'Não foi possível registrar o movimento.'))
      }

      return data
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: partsKey }),
  })
}

/** Deactivating is a soft delete: service_order_items point at this row. */
export function useSetPartActive() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async ({ id, active }: { id: string; active: boolean }) => {
      const { error } = active
        ? await api.POST('/api/parts/{id}/reactivate', { params: { path: { id } } })
        : await api.DELETE('/api/parts/{id}', { params: { path: { id } } })

      if (error) {
        throw new Error(
          messageFrom(error, active ? 'Não foi possível reativar.' : 'Não foi possível desativar.'),
        )
      }
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: partsKey }),
  })
}

// ---------------------------------------------------------------------------
// Leitura das etiquetas do protótipo
// ---------------------------------------------------------------------------

export type StockLabel = {
  text: string
  /** Classes do Tailwind para a etiqueta, nas cores do desenho. */
  className: string
}

/**
 * The three badges the prototype draws — "Em estoque", "Estoque mínimo" and
 * "Sem estoque" — plus the fourth state the design never anticipated: a
 * negative balance, which D-41 allows when an order is finished with parts the
 * shelf did not have. It reads as a debt, not as an empty shelf.
 */
export function stockLabel(part: Part): StockLabel {
  if (part.quantityOnHand < 0) {
    return { text: 'Estoque negativo', className: 'bg-red-100 text-red-700 ring-1 ring-red-300' }
  }

  if (part.quantityOnHand === 0) {
    return { text: 'Sem estoque', className: 'bg-red-100 text-red-500' }
  }

  if (part.isLowStock) {
    return { text: 'Estoque mínimo', className: 'bg-amber-100 text-amber-600' }
  }

  return { text: 'Em estoque', className: 'bg-emerald-100 text-emerald-600' }
}

const money = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })

export function formatMoney(value: number): string {
  return money.format(value)
}

/** Quantity is numeric(10,3): whole units read as "12", fractions keep them. */
export function formatQuantity(value: number): string {
  return new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 3 }).format(value)
}

export const movementLabel: Record<MovementType, string> = {
  IN: 'Entrada',
  OUT: 'Saída',
  ADJUSTMENT: 'Ajuste',
}
