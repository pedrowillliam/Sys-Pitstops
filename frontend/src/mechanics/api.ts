import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../api/client'
import type { components } from '../api/schema'

export type MechanicWorkload = components['schemas']['MechanicWorkload']
export type QueuedOrder = components['schemas']['QueuedOrder']
export type UserInput = components['schemas']['CreateUserRequest']
export type UserRole = components['schemas']['UserRole']

/** A mesma raiz que `useMechanics` em service-orders/api.ts usa para a lista de
 *  nomes: invalidar aqui renova as duas, e um mecânico recém-cadastrado aparece
 *  no cartão e no seletor da OS ao mesmo tempo. */
export const mechanicsKey = ['mechanics'] as const

function messageFrom(error: unknown, fallback: string): string {
  const problem = error as { title?: string; detail?: string } | undefined
  return problem?.detail ?? problem?.title ?? fallback
}

/** Um cartão por mecânico ativo: ocupado ou livre, o carro em mãos, o próximo
 *  e o tamanho da fila. A regra mora no servidor (MechanicQueue), para que o
 *  cartão nunca discorde do quadro. */
export function useWorkloads() {
  return useQuery({
    queryKey: [...mechanicsKey, 'workload'],
    queryFn: async () => {
      const { data, error } = await api.GET('/api/mechanics')

      if (error || !data) {
        throw new Error(messageFrom(error, 'Não foi possível carregar os mecânicos.'))
      }

      return data
    },
  })
}

export function useCreateUser() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (input: UserInput) => {
      const { data, error } = await api.POST('/api/users', { body: input })

      if (error || !data) {
        throw new Error(messageFrom(error, 'Não foi possível cadastrar o funcionário.'))
      }

      return data
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: mechanicsKey }),
  })
}

/** Os rótulos do seletor "Cargo / Função" do protótipo, na ordem em que a
 *  oficina contrata: quem trabalha no pátio primeiro. */
export const roleOptions: { value: UserRole; label: string }[] = [
  { value: 'MECHANIC', label: 'Mecânico' },
  { value: 'ATTENDANT', label: 'Atendente' },
  { value: 'ADMIN', label: 'Administrador' },
]
