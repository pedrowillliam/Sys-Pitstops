import type { ReactNode } from 'react'
import { icons, type IconName } from './iconMap'

/**
 * The dark strip every content screen of the prototype opens with: the section
 * icon, its name, and room on the right for the screen's primary action.
 *
 * (In the Figma frame for Estoque the strip still reads "Orçamentos" — a
 * leftover from duplicating the previous screen. The section's own name is what
 * goes here.)
 */
export function PageHeader({
  icon,
  title,
  actions,
}: {
  icon: IconName
  title: string
  actions?: ReactNode
}) {
  const Icon = icons[icon]

  return (
    <div className="mb-6">
      <div className="flex items-center gap-3 rounded-xl bg-brand-deep px-5 py-3.5 text-white">
        <Icon className="h-5 w-5 shrink-0" />
        <h1 className="truncate text-base font-semibold">{title}</h1>
      </div>

      {actions && <div className="mt-5 flex flex-wrap justify-end gap-3">{actions}</div>}
    </div>
  )
}
