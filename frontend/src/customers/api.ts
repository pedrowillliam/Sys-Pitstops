import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../api/client'
import type { components } from '../api/schema'

export type Customer = components['schemas']['CustomerResponse']
export type CustomerInput = components['schemas']['CustomerRequest']

export type CustomerFilters = {
  search: string
  includeInactive: boolean
  page: number
}

export const customersKey = ['customers'] as const

// The backend clamps pageSize anyway (PagedResult.Clamp); this is only the size
// the list asks for.
export const pageSize = 20

/** ProblemDetails carries the message the API already wrote in Portuguese;
 *  falling back to a generic sentence keeps a null title from reaching the UI. */
function messageFrom(error: unknown, fallback: string): string {
  const problem = error as { title?: string; detail?: string } | undefined
  return problem?.detail ?? problem?.title ?? fallback
}

export function useCustomers(filters: CustomerFilters) {
  return useQuery({
    queryKey: [...customersKey, 'list', filters],
    queryFn: async () => {
      const { data, error } = await api.GET('/api/customers', {
        params: {
          query: {
            search: filters.search || undefined,
            includeInactive: filters.includeInactive,
            page: filters.page,
            pageSize,
          },
        },
      })

      if (error || !data) {
        throw new Error(messageFrom(error, 'Não foi possível carregar os clientes.'))
      }

      return data
    },
    // Keeps the previous page on screen while the next one loads, so typing in
    // the search box does not blank the table on every keystroke.
    placeholderData: (previous) => previous,
  })
}

export function useCustomer(id: string | undefined) {
  return useQuery({
    queryKey: [...customersKey, 'one', id],
    enabled: Boolean(id),
    queryFn: async () => {
      const { data, error } = await api.GET('/api/customers/{id}', {
        params: { path: { id: id as string } },
      })

      if (error || !data) {
        throw new Error(messageFrom(error, 'Não foi possível carregar o cliente.'))
      }

      return data
    },
  })
}

export function useSaveCustomer(id?: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (input: CustomerInput) => {
      const { data, error } = id
        ? await api.PUT('/api/customers/{id}', {
            params: { path: { id } },
            body: input,
          })
        : await api.POST('/api/customers', { body: input })

      if (error || !data) {
        throw new Error(messageFrom(error, 'Não foi possível salvar o cliente.'))
      }

      return data
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: customersKey }),
  })
}

/** Deactivating is a soft delete: the row stays, so the service orders that
 *  point at this customer keep resolving (D-08). */
export function useSetCustomerActive() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async ({ id, active }: { id: string; active: boolean }) => {
      const { error } = active
        ? await api.POST('/api/customers/{id}/reactivate', { params: { path: { id } } })
        : await api.DELETE('/api/customers/{id}', { params: { path: { id } } })

      if (error) {
        throw new Error(
          messageFrom(error, active ? 'Não foi possível reativar.' : 'Não foi possível desativar.'),
        )
      }
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: customersKey }),
  })
}
