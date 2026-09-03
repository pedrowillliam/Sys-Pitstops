import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, type AuthenticatedUser } from '../api/client'

export const sessionKey = ['session'] as const

// `null` means "asked, and nobody is logged in" — an ordinary answer, not an
// error. Keeping the 401 out of the error channel is what lets the router send
// the visitor to the login screen instead of an error screen.
export function useSession() {
  return useQuery<AuthenticatedUser | null>({
    queryKey: sessionKey,
    queryFn: async () => {
      const { data, error, response } = await api.GET('/api/auth/me')

      if (response.status === 401) {
        return null
      }

      if (error || !data) {
        throw new Error('Não foi possível verificar a sessão.')
      }

      return data
    },
    retry: false,
    staleTime: 5 * 60 * 1000,
  })
}

export function useLogin() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (credentials: { email: string; password: string }) => {
      const { data, error } = await api.POST('/api/auth/login', { body: credentials })

      if (error || !data) {
        throw new Error('E-mail ou senha incorretos.')
      }

      return data
    },
    onSuccess: (user) => {
      queryClient.setQueryData(sessionKey, user)
    },
  })
}

export function useLogout() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async () => {
      await api.POST('/api/auth/logout')
    },
    // The cookie is gone either way, so the cached reads have to go with it —
    // otherwise the next person to sign in on the same machine opens the
    // previous user's screens (D-16 caches reads, including these).
    onSettled: () => {
      queryClient.removeQueries()
      queryClient.setQueryData(sessionKey, null)
    },
  })
}
