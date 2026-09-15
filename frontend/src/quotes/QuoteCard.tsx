import type { ServiceOrderDetail } from '../service-orders/api'
import { useOrderQuotes, useSendQuote, type OrderQuote } from './api'
import { money } from './publicApi'
import { useWorkshop } from '../settings/api'
import { hasWhatsApp, isLocalOrigin, publicQuoteUrl, whatsAppLink } from './whatsapp'

const statusLabels: Record<OrderQuote['status'], string> = {
  SENT: 'Aguardando resposta',
  APPROVED: 'Aprovado',
  REJECTED: 'Recusado',
  EXPIRED: 'Expirado',
}

/** O orçamento nasce da OS, ao lado dos itens que o compõem (D-36). */
export function QuoteCard({ order }: { order: ServiceOrderDetail }) {
  const { data: quotes, isPending } = useOrderQuotes(order.id)
  const { data: workshop } = useWorkshop()
  const send = useSendQuote()

  const live = quotes?.find((quote) => quote.status === 'SENT')
  const empty = order.items.length === 0
  const sendable = hasWhatsApp(order.customerPhone)

  function linkFor(quote: OrderQuote): string {
    return whatsAppLink({
      workshopName: workshop?.name ?? 'oficina',
      expiresAt: quote.expiresAt,
      customerName: order.customerName,
      customerPhone: order.customerPhone,
      serviceOrderNumber: order.number,
      vehicleDescription: order.vehicleDescription,
      vehiclePlate: order.vehiclePlate,
      totalAmount: quote.totalAmount,
      publicToken: quote.publicToken,
    })
  }

  return (
    <div className="rounded-lg border border-line bg-panel p-4">
      <h2 className="text-sm font-semibold">Orçamento</h2>

      {isPending ? (
        <p className="mt-3 text-sm text-ink-soft">Carregando…</p>
      ) : (
        <>
          {empty ? (
            <p className="mt-3 text-sm text-ink-soft">
              Lance ao menos um item para poder enviar o orçamento.
            </p>
          ) : (
            <p className="mt-3 text-sm text-ink-soft">
              Vai ao cliente o total de <strong className="text-ink">{money(order.total)}</strong>,
              com os {order.items.length} {order.items.length === 1 ? 'item' : 'itens'} lançados.
            </p>
          )}

          {/* D-10: o orçamento congela os itens. Reenviar não conserta o que já
              saiu — a API expira o link de pé e cria outro. */}
          <button
            type="button"
            disabled={empty || send.isPending}
            onClick={() => send.mutate(order.id)}
            className="mt-3 rounded bg-brand px-4 py-2 text-sm font-medium text-white disabled:opacity-40"
          >
            {send.isPending ? 'Gerando…' : live ? 'Gerar novo orçamento' : 'Gerar orçamento'}
          </button>

          {send.isError && (
            <p role="alert" className="mt-2 text-sm">
              {(send.error as Error).message}
            </p>
          )}

          {quotes && quotes.length > 0 && (
            <>
              <h3 className="mt-4 text-sm font-medium">Enviados</h3>
              {isLocalOrigin() && (
                <p className="mt-1 text-xs text-amber-800">
                  Ambiente local: o link só abre nesta máquina.
                </p>
              )}
              <ul className="mt-2 space-y-2">
                {quotes.map((quote) => (
                  <li key={quote.id} className="rounded border border-line px-3 py-2 text-sm">
                    <div className="flex justify-between gap-3">
                      <span>{money(quote.totalAmount)}</span>
                      <span className="text-ink-soft">{statusLabels[quote.status]}</span>
                    </div>

                    {quote.status === 'SENT' && (
                      <div className="mt-2 flex flex-wrap gap-2">
                        {sendable ? (
                          <a
                            href={linkFor(quote)}
                            target="_blank"
                            rel="noreferrer"
                            className="rounded bg-emerald-600 px-3 py-1 text-xs font-medium text-white"
                          >
                            Enviar no WhatsApp
                          </a>
                        ) : (
                          <span className="self-center text-xs text-ink-soft">
                            O cadastro não tem celular — envie o link por outro meio.
                          </span>
                        )}
                        <button
                          type="button"
                          onClick={() =>
                            navigator.clipboard.writeText(publicQuoteUrl(quote.publicToken))
                          }
                          className="rounded border border-line px-3 py-1 text-xs font-medium hover:bg-surface"
                        >
                          Copiar link
                        </button>
                      </div>
                    )}

                    {quote.status === 'REJECTED' && quote.rejectionReason && (
                      <p className="mt-1 text-xs text-ink-soft">
                        Motivo: {quote.rejectionReason}
                      </p>
                    )}
                  </li>
                ))}
              </ul>
            </>
          )}
        </>
      )}
    </div>
  )
}
