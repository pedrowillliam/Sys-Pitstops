/**
 * The prototype draws the rail with one icon per section. They are inline
 * instead of a library because there are nine of them — a dependency would
 * weigh more than the file.
 *
 * Every icon is a 24×24 stroke drawing so they line up at any size; `className`
 * carries the size and the colour from the caller.
 */
type IconProps = { className?: string }

function Svg({ className, children }: IconProps & { children: React.ReactNode }) {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={1.8}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      className={className}
    >
      {children}
    </svg>
  )
}

export function TrendingUpIcon(props: IconProps) {
  return (
    <Svg {...props}>
      <path d="M3 17 9 11l4 4 8-8" />
      <path d="M15 4h6v6" />
    </Svg>
  )
}

export function BoardIcon(props: IconProps) {
  return (
    <Svg {...props}>
      <rect x="3" y="3" width="18" height="18" rx="2" />
      <path d="M9 3v18M15 3v9" />
    </Svg>
  )
}

export function WrenchIcon(props: IconProps) {
  return (
    <Svg {...props}>
      <path d="M15.5 3a5.5 5.5 0 0 0-5.2 7.3L3 17.6V21h3.4l7.3-7.3A5.5 5.5 0 1 0 15.5 3Z" />
      <path d="M17.5 6.5h.01" />
    </Svg>
  )
}

export function UsersIcon(props: IconProps) {
  return (
    <Svg {...props}>
      <circle cx="9" cy="8" r="3.2" />
      <path d="M3 20a6 6 0 0 1 12 0" />
      <path d="M16 5.3a3.2 3.2 0 0 1 0 5.4M18 20a6 6 0 0 0-2.4-4.8" />
    </Svg>
  )
}

export function CurrencyIcon(props: IconProps) {
  return (
    <Svg {...props}>
      <path d="M12 2v20" />
      <path d="M17 6.5A3.5 3.5 0 0 0 13.5 4h-2a3.5 3.5 0 0 0 0 7h1a3.5 3.5 0 0 1 0 7h-2A3.5 3.5 0 0 1 7 15.5" />
    </Svg>
  )
}

export function BoxIcon(props: IconProps) {
  return (
    <Svg {...props}>
      <path d="M21 8.2v7.6a1.6 1.6 0 0 1-.85 1.41l-7 3.73a1.6 1.6 0 0 1-1.5 0l-7-3.73A1.6 1.6 0 0 1 3 15.8V8.2a1.6 1.6 0 0 1 .85-1.41l7-3.73a1.6 1.6 0 0 1 1.5 0l7 3.73A1.6 1.6 0 0 1 21 8.2Z" />
      <path d="m3.3 7.3 8.7 4.6 8.7-4.6M12 21v-9.1" />
    </Svg>
  )
}

export function GearIcon(props: IconProps) {
  return (
    <Svg {...props}>
      <circle cx="12" cy="12" r="3.2" />
      <path d="M19.4 14a1.6 1.6 0 0 0 .32 1.77l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06A1.6 1.6 0 0 0 15.12 18a1.6 1.6 0 0 0-.97 1.47V20a2 2 0 1 1-4 0v-.1A1.6 1.6 0 0 0 9.1 18.4a1.6 1.6 0 0 0-1.77.32l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06A1.6 1.6 0 0 0 4.82 14a1.6 1.6 0 0 0-1.47-.97H3a2 2 0 1 1 0-4h.1A1.6 1.6 0 0 0 4.6 8.1a1.6 1.6 0 0 0-.32-1.77l-.06-.06a2 2 0 1 1 2.83-2.83l.06.06A1.6 1.6 0 0 0 8.88 4h.07A1.6 1.6 0 0 0 10 2.6V2a2 2 0 1 1 4 0v.1a1.6 1.6 0 0 0 .97 1.47 1.6 1.6 0 0 0 1.77-.32l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06A1.6 1.6 0 0 0 19.4 8.9v.07A1.6 1.6 0 0 0 20.8 10h.2a2 2 0 1 1 0 4h-.1a1.6 1.6 0 0 0-1.47.97Z" />
    </Svg>
  )
}

export function ClipboardIcon(props: IconProps) {
  return (
    <Svg {...props}>
      <rect x="5" y="4" width="14" height="17" rx="2" />
      <path d="M9 4a2 2 0 0 1 2-2h2a2 2 0 0 1 2 2M9 11h6M9 15h4" />
    </Svg>
  )
}

export function SearchIcon(props: IconProps) {
  return (
    <Svg {...props}>
      <circle cx="11" cy="11" r="7" />
      <path d="m20 20-3.5-3.5" />
    </Svg>
  )
}

export function BellIcon(props: IconProps) {
  return (
    <Svg {...props}>
      <path d="M18 9a6 6 0 1 0-12 0c0 5-2 6-2 6h16s-2-1-2-6Z" />
      <path d="M13.7 20a2 2 0 0 1-3.4 0" />
    </Svg>
  )
}

export function PlusCircleIcon(props: IconProps) {
  return (
    <Svg {...props}>
      <circle cx="12" cy="12" r="9" />
      <path d="M12 8v8M8 12h8" />
    </Svg>
  )
}

export function ChevronDownIcon(props: IconProps) {
  return (
    <Svg {...props}>
      <path d="m6 9 6 6 6-6" />
    </Svg>
  )
}

export function MenuIcon(props: IconProps) {
  return (
    <Svg {...props}>
      <path d="M4 7h16M4 12h16M4 17h16" />
    </Svg>
  )
}
