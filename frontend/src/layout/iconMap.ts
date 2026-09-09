import {
  BoardIcon,
  BoxIcon,
  ClipboardIcon,
  CurrencyIcon,
  GearIcon,
  TrendingUpIcon,
  UsersIcon,
  WrenchIcon,
} from './icons'

/** Separado de `icons.tsx` porque um módulo que exporta componentes não deve
 *  exportar mais nada — o fast refresh do Vite desliga quando isso acontece. */
export const icons = {
  dashboard: TrendingUpIcon,
  board: BoardIcon,
  wrench: WrenchIcon,
  users: UsersIcon,
  currency: CurrencyIcon,
  box: BoxIcon,
  gear: GearIcon,
  clipboard: ClipboardIcon,
} as const

export type IconName = keyof typeof icons
