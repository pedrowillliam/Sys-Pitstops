import type { Role } from '../api/client'
import type { IconName } from './iconMap'

export type NavItem = {
  to: string
  label: string
  icon: IconName
  /** Empty means every signed-in role sees it. */
  roles: Role[]
  /** False while the screen is still a placeholder. */
  ready: boolean
  /** The prototype parks this one alone at the bottom of the rail. */
  atFoot?: boolean
}

// Espelha o menu lateral do protótipo no Figma (arquivo "Sys-PitStop"): o
// administrador vê Dashboard no topo, o atendente vê a mesma lista sem ele, e a
// engrenagem fica separada no pé do rail. Os rótulos são os do desenho.
// Os caminhos ficam em inglês como as rotas da API (CLAUDE.md); o rótulo que o
// mecânico lê é que é em português.
export const navigation: NavItem[] = [
  { to: '/dashboard', label: 'Dashboard', icon: 'dashboard', roles: ['ADMIN'], ready: true },
  { to: '/service-orders', label: 'Kanban', icon: 'board', roles: [], ready: true },
  { to: '/my-queue', label: 'Minha fila', icon: 'clipboard', roles: ['MECHANIC'], ready: true },
  { to: '/mechanics', label: 'Mecânicos', icon: 'wrench', roles: ['ADMIN', 'ATTENDANT'], ready: true },
  {
    to: '/customers',
    label: 'Clientes Inscritos',
    icon: 'users',
    roles: ['ADMIN', 'ATTENDANT'],
    ready: true,
  },
  { to: '/quotes', label: 'Orçamento', icon: 'currency', roles: ['ADMIN', 'ATTENDANT'], ready: true },
  { to: '/inventory', label: 'Estoque', icon: 'box', roles: ['ADMIN', 'ATTENDANT'], ready: true },
  {
    to: '/settings',
    label: 'Configurações',
    icon: 'gear',
    roles: ['ADMIN'],
    ready: false,
    atFoot: true,
  },
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
