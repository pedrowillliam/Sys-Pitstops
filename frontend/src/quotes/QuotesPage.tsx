import { useState } from 'react'
import { pageSize, useQuotes, type Quote, type QuoteStatus } from './api'
import { money } from './publicApi'
import { hasWhatsApp, publicQuoteUrl, whatsAppLink } from './whatsapp'

const filters: { key: QuoteStatus | 'ALL'; label: string }[] = [
  { key: 'ALL', label: 'Todos' },
  { key: 'SENT', label: 'Aguardando' },
  { key: 'APPROVED', label: 'Aprovados' },
  { key: 'REJECTED', label: 'Recusados' },
  { key: 'EXPIRED', label: 'Expirados' },
]

const badges: Record<QuoteStatus, { label: string; className: string }> = {
  SENT: { label: 'Aguardando', className: 'bg-amber-100 text-amber-800' },
  APPROVED: { label: 'Aprovado', className: 'bg-emerald-100 text-emerald-800' },
  REJECTED: { label: 'Recusado', className: 'bg-rose-100 text-rose-800' },
  EXPIRED: { label: 'Expirado', className: 'bg-line text-ink-soft' },
}

/** "15m", "2h", "10d" — o protótipo mostra há quanto tempo o orçamento saiu,
 *  que é o que diz se vale a pena cobrar uma resposta. */
function ago(iso: string): string {
  const minutes = Math.max(0, Math.round((Date.now() - new Date(iso).getTime()) / 60000))

  if (minutes < 60) {
    return `${minutes}m`
  }

  return minutes < 1440 ? `${Math.floor(minutes / 60)}h` : `${Math.floor(minutes / 1440)}d`
}

function QuoteRow({ quote }: { quote: Quote }) {
  const [copied, setCopied] = useState(false)
  const badge = badges[quote.status]

  async function copyLink() {
    await navigator.clipboard.writeText(publicQuoteUrl(quote.publicToken))
    setCopied(true)
    setTimeout(() => setCopied(false), 2000)
  }

  return (
    <li className="flex flex-wrap items-center gap-3 rounded-lg border border-line bg-panel px-4 py-3">
      <span className="w-10 shrink-0 text-center text-xs text-ink-soft">{ago(quote.sentAt)}</span>

      <div className="min-w-56 grow">
        <p className="text-sm text-ink-soft">
          OS-{quote.serviceOrderNumber} · Orçamento para{' '}
          <span className="font-semibold text-ink">{quote.customerName}</span>{' '}
          ({quote.vehicleDescription} · {quote.vehiclePlate})
        </p>

        <p className="mt-0.5 flex items-center gap-2">
          <span className="font-semibold">{money(quote.totalAmount)}</span>
          <span className={`rounded px-2 py-0.5 text-xs font-medium ${badge.className}`}>
            {badge.label}
          </span>
        </p>
      </div>

      <div className="flex shrink-0 gap-2">
        <button
          type="button"
          onClick={copyLink}
          className="rounded border border-line px-3 py-1 text-xs font-medium hover:bg-surface"
        >
          {copied ? 'Copiado!' : 'Copiar link'}
        </button>

        {/* Um link já respondido ou vencido não leva a lugar nenhum, e telefone
            fixo não recebe mensagem: o botão só aparece quando os dois cabem. */}
        {quote.status === 'SENT' &&
          (hasWhatsApp(quote.customerPhone) ? (
            <a
              href={whatsAppLink(quote)}
              target="_blank"
              rel="noreferrer"
              className="rounded bg-emerald-600 px-3 py-1 text-xs font-medium text-white"
            >
              WhatsApp
            </a>
          ) : (
            <span className="self-center text-xs text-ink-soft">
              Sem celular no cadastro
            </span>
          ))}
      </div>
    </li>
  )
}

export function QuotesPage() {
  const [status, setStatus] = useState<QuoteStatus | 'ALL'>('ALL')
  const [page, setPage] = useState(1)

  const { data, isPending, isError, error } = useQuotes(status, page)
  const quotes = data?.items ?? []
  const lastPage = Math.max(1, Math.ceil((data?.total ?? 0) / pageSize))

  function filterBy(key: QuoteStatus | 'ALL') {
    setStatus(key)
    setPage(1)
  }

  return (
    <section>
      <header className="rounded bg-brand px-4 py-3 text-sm font-medium text-white">
        Orçamentos
      </header>

      <div className="mt-4 flex flex-wrap gap-2">
        {filters.map((filter) => (
          <button
            key={filter.key}
            type="button"
            onClick={() => filterBy(filter.key)}
            aria-pressed={status === filter.key}
            className={`rounded border px-3 py-1 text-sm ${
              status === filter.key
                ? 'border-brand bg-brand text-white'
                : 'border-line bg-panel hover:bg-surface'
            }`}
          >
            {filter.label}
          </button>
        ))}
      </div>

      <div className="mt-4 rounded-lg border border-line bg-panel p-4">
        <h1 className="text-lg font-semibold">Atualizações de Orçamentos</h1>

        {isError && (
          <p role="alert" className="mt-4 text-sm">
            {(error as Error).message}
          </p>
        )}

        {isPending ? (
          <p className="mt-4 text-sm text-ink-soft">Carregando…</p>
        ) : quotes.length === 0 ? (
          <p className="mt-4 text-sm text-ink-soft">
            Nenhum orçamento aqui. Eles são gerados a partir da ordem de serviço, no quadro.
          </p>
        ) : (
          <ul className="mt-4 space-y-3">
            {quotes.map((quote) => (
              <QuoteRow key={quote.id} quote={quote} />
            ))}
          </ul>
        )}

        {lastPage > 1 && (
          <div className="mt-4 flex items-center justify-center gap-3 text-sm">
            <button
              type="button"
              disabled={page <= 1}
              onClick={() => setPage((current) => current - 1)}
              className="rounded border border-line px-3 py-1 disabled:opacity-40"
            >
              Anterior
            </button>
            <span className="text-ink-soft">
              {page} de {lastPage}
            </span>
            <button
              type="button"
              disabled={page >= lastPage}
              onClick={() => setPage((current) => current + 1)}
              className="rounded border border-line px-3 py-1 disabled:opacity-40"
            >
              Próxima
            </button>
          </div>
        )}
      </div>
    </section>
  )
}
