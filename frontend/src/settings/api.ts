import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../api/client'
import type { components } from '../api/schema'

export type Workshop = components['schemas']['WorkshopResponse']
export type WorkshopInput = components['schemas']['WorkshopRequest']
export type ChangePasswordInput = components['schemas']['ChangePasswordRequest']

const workshopKey = ['workshop'] as const

/** O corpo de erro de validação traz um campo por chave; a tela mostra a
 *  primeira mensagem, que é a que explica o que corrigir. */
function messageFrom(error: unknown, fallback: string): string {
  const problem = error as
    | { title?: string; detail?: string; errors?: Record<string, string[]> }
    | undefined

  const field = Object.values(problem?.errors ?? {})[0]?.[0]

  return field ?? problem?.detail ?? problem?.title ?? fallback
}

export function useWorkshop() {
  return useQuery({
    queryKey: workshopKey,
    queryFn: async () => {
      const { data, error } = await api.GET('/api/workshop')

      if (error || !data) {
        throw new Error(messageFrom(error, 'Não foi possível carregar a oficina.'))
      }

      return data
    },
  })
}

export function useSaveWorkshop() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (input: WorkshopInput) => {
      const { data, error } = await api.PUT('/api/workshop', { body: input })

      if (error || !data) {
        throw new Error(messageFrom(error, 'Não foi possível salvar a oficina.'))
      }

      return data
    },
    onSuccess: (workshop) => queryClient.setQueryData(workshopKey, workshop),
  })
}

export function useChangePassword() {
  return useMutation({
    mutationFn: async (input: ChangePasswordInput) => {
      const { error } = await api.POST('/api/auth/password', { body: input })

      if (error) {
        throw new Error(messageFrom(error, 'Não foi possível trocar a senha.'))
      }
    },
  })
}
