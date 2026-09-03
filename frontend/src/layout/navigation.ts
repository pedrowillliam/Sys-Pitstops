import type { Role } from '../api/client'

export type NavItem = {
  to: string
  label: string
  /** Empty means every signed-in role sees it. */
  roles: Role[]
  /** False while the screen is still a placeholder. */
  ready: boolean
}

// Espelha o menu lateral do protótipo no Figma (arquivo "Sys-PitStop"): o
// administrador vê Dashboard no topo, o atendente vê a mesma lista sem ele.
// Os caminhos ficam em inglês como as rotas da API (CLAUDE.md); o rótulo que o
// mecânico lê é que é em português.
export const navigation: NavItem[] = [
  { to: '/dashboard', label: 'Dashboard', roles: ['ADMIN'], ready: false },
  { to: '/service-orders', label: 'Kanban', roles: [], ready: false },
  { to: '/my-queue', label: 'Minha fila', roles: ['MECHANIC'], ready: false },
  { to: '/mechanics', label: 'Mecânicos', roles: ['ADMIN', 'ATTENDANT'], ready: false },
  { to: '/customers', label: 'Clientes', roles: ['ADMIN', 'ATTENDANT'], ready: false },
  { to: '/quotes', label: 'Orçamento', roles: ['ADMIN', 'ATTENDANT'], ready: false },
  { to: '/inventory', label: 'Estoque', roles: ['ADMIN', 'ATTENDANT'], ready: false },
  { to: '/settings', label: 'Configurações', roles: ['ADMIN'], ready: false },
]

export function visibleTo(role: string): NavItem[] {
  return navigation.filter(
    (item) => item.roles.length === 0 || item.roles.includes(role as Role),
  )
}

// Onde cada papel cai depois de entrar. O protótipo dá ao mecânico só telas
// mobile, todas em cima da fila dele; os demais trabalham a partir do quadro.
export function landingFor(role: string): string {
  return role === 'MECHANIC' ? '/my-queue' : '/service-orders'
}
