import { Link } from 'react-router'

// Every section of the MVP already has a route and a place in the menu; only
// the screens are missing. A named placeholder makes what is left visible in
// the app itself instead of only in the backlog.
export function Placeholder({ title, waitingFor }: { title: string; waitingFor: string }) {
  return (
    <section className="mx-auto max-w-2xl">
      <h1 className="text-2xl font-semibold">{title}</h1>
      <p className="mt-3 rounded border border-line bg-panel p-4 text-sm text-ink-soft">
        Tela ainda não construída. Depende de: {waitingFor}.
      </p>
    </section>
  )
}

export function NotFound() {
  return (
    <section className="mx-auto max-w-2xl">
      <h1 className="text-2xl font-semibold">Página não encontrada</h1>
      <p className="mt-3 text-sm text-ink-soft">
        <Link to="/" className="underline underline-offset-2">
          Voltar para o início
        </Link>
      </p>
    </section>
  )
}
