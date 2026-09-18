import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../api/client'
import { customersKey } from './api'
import type { components } from '../api/schema'

export type Vehicle = components['schemas']['VehicleResponse']
export type VehicleInput = components['schemas']['VehicleRequest']

const vehiclesKey = (ownerId: string) => ['vehicles', ownerId] as const

function messageFrom(error: unknown, fallback: string): string {
  const problem = error as
    | { title?: string; detail?: string; errors?: Record<string, string[]> }
    | undefined

  const field = Object.values(problem?.errors ?? {})[0]?.[0]

  return field ?? problem?.detail ?? problem?.title ?? fallback
}

/** Os carros de um cliente. A lista de clientes mostra os veículos de cada um,
 *  então salvar aqui invalida as duas consultas. */
export function useVehicles(ownerId: string) {
  return useQuery({
    queryKey: vehiclesKey(ownerId),
    queryFn: async () => {
      const { data, error } = await api.GET('/api/vehicles', {
        params: { query: { ownerId, pageSize: 100 } },
      })

      if (error || !data) {
        throw new Error(messageFrom(error, 'Não foi possível carregar os veículos.'))
      }

      return data.items
    },
  })
}

export function useSaveVehicle(ownerId: string) {
  const queryClient = useQueryClient()

  async function refresh() {
    await queryClient.invalidateQueries({ queryKey: vehiclesKey(ownerId) })
    await queryClient.invalidateQueries({ queryKey: customersKey })
  }

  return useMutation({
    mutationFn: async ({ id, ...body }: VehicleInput & { id?: string }) => {
      const { data, error } = id
        ? await api.PUT('/api/vehicles/{id}', { params: { path: { id } }, body })
        : await api.POST('/api/vehicles', { body })

      if (error || !data) {
        throw new Error(messageFrom(error, 'Não foi possível salvar o veículo.'))
      }

      return data
    },
    onSuccess: refresh,
  })
}

export function useRemoveVehicle(ownerId: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (id: string) => {
      const { error } = await api.DELETE('/api/vehicles/{id}', { params: { path: { id } } })

      if (error) {
        // O servidor recusa excluir veículo com OS, para não apagar histórico:
        // a mensagem dele é mais útil que qualquer genérica daqui.
        throw new Error(messageFrom(error, 'Não foi possível remover o veículo.'))
      }
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: vehiclesKey(ownerId) })
      await queryClient.invalidateQueries({ queryKey: customersKey })
    },
  })
}

/** "Fiat Strada 2021" — o ano é opcional no banco. */
export function describeVehicle(vehicle: { brand: string; model: string; modelYear?: number | null }) {
  return [vehicle.brand, vehicle.model, vehicle.modelYear].filter(Boolean).join(' ')
}

/** Placas antigas são AAA0000 e as do Mercosul AAA0A00. A máscara deixa em
 *  maiúsculas e corta em sete, que é o que as duas têm em comum — a validação
 *  do formato é do servidor. */
export function formatPlate(value: string): string {
  return value.toUpperCase().replace(/[^A-Z0-9]/g, '').slice(0, 7)
}
