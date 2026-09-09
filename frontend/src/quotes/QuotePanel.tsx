import { useOrder } from '../service-orders/api'
import { useOrderQuotes, useSendQuote, type OrderQuote } from './api'
import { money } from './publicApi'
import { hasWhatsApp, isLocalOrigin, publicQuoteUrl, whatsAppLink } from './whatsapp'

const statusLabels: Record<OrderQuote['status'], string> = {
  SENT: 'Aguardando resposta',
  APPROVED: 'Aprovado',
  REJECTED: 'Recusado',
  EXPIRED: 'Expirado',
}

export function QuotePanel({ orderId, onClose }: { orderId: string; onClose: () => void }) {
  const { data: order, isPending: loadingOrder } = useOrder(orderId)
  const { data: quotes, isPending: loadingQuotes } = useOrderQuotes(orderId)
  const send = useSendQuote()

  const live = quotes?.find((quote) => quote.status === 'SENT')
  const empty = order?.items.length === 0
  const sendable = hasWhatsApp(order?.customerPhone ?? '')

  function linkFor(quote: OrderQuote): string {
    return whatsAppLink({
      customerName: order?.customerName ?? '',
      customerPhone: order?.customerPhone ?? '',
      serviceOrderNumber: order?.number ?? 0,
      vehicleDescription: order?.vehicleDescription ?? '',
      vehiclePlate: order?.vehiclePlate ?? '',
      totalAmount: quote.totalAmount,
      publicToken: quote.publicToken,
    })
  }

  return (
    <div className="fixed inset-0 z-10 flex items-center justify-center bg-black/40 p-4">
      <div className="max-h-full w-full max-w-lg overflow-y-auto rounded-lg bg-panel p-5">
        <h2 className="text-lg font-semibold">
          Orçamento da OS-{order?.number ?? '…'}
        </h2>

        {loadingOrder || loadingQuotes ? (
          <p className="mt-4 text-sm text-ink-soft">Carregando…</p>
        ) : (
          <>
            <p className="mt-1 text-sm text-ink-soft">
              {order?.customerName} · {order?.vehicleDescription} · {order?.vehiclePlate}
            </p>

            <h3 className="mt-4 text-sm font-semibold">O que vai no orçamento</h3>

            {empty ? (
              <p className="mt-1 text-sm text-ink-soft">
                A OS ainda não tem itens lançados. Lance ao menos um para enviar o orçamento.
              </p>
            ) : (
              <ul className="mt-1 divide-y divide-line text-sm">
                {order?.items.map((item) => (
                  <li key={item.id} className="flex justify-between gap-3 py-1">
                    <span>
                      {item.description}
                      <span className="text-ink-soft"> × {item.quantity}</span>
                    </span>
                    <span>{money(item.total)}</span>
                  </li>
                ))}
              </ul>
            )}

            {order && order.discountAmount > 0 && (
              <p className="mt-1 flex justify-between text-sm text-ink-soft">
                <span>Desconto</span>
                <span>-{money(order.discountAmount)}</span>
              </p>
            )}

            <p className="mt-2 flex justify-between font-semibold">
              <span>Total</span>
              <span>{money(order?.total ?? 0)}</span>
            </p>

            {/* D-10: o orçamento congela os itens. Reenviar não conserta o que
                já saiu — a API expira o link de pé e cria outro. */}
            <button
              type="button"
              disabled={empty || send.isPending}
              onClick={() => send.mutate(orderId)}
              className="mt-4 w-full rounded bg-brand px-4 py-2 text-sm font-medium text-white disabled:opacity-40"
            >
              {send.isPending
                ? 'Gerando…'
                : live
                  ? 'Gerar novo orçamento'
                  : 'Gerar orçamento'}
            </button>

            {send.isError && (
              <p role="alert" className="mt-3 text-sm">
                {(send.error as Error).message}
              </p>
            )}

            {quotes && quotes.length > 0 && (
              <>
                <h3 className="mt-5 text-sm font-semibold">Enviados</h3>
                {isLocalOrigin() && (
                  <p className="mt-1 text-xs text-amber-800">
                    Ambiente local: o link só abre nesta máquina.
                  </p>
                )}
                <ul className="mt-1 space-y-2">
                  {quotes.map((quote) => (
                    <li
                      key={quote.id}
                      className="rounded border border-line px-3 py-2 text-sm"
                    >
                      <div className="flex justify-between gap-3">
                        <span>{money(quote.totalAmount)}</span>
                        <span className="text-ink-soft">{statusLabels[quote.status]}</span>
                      </div>

                      {quote.status === 'SENT' && (
                        <div className="mt-2 flex flex-wrap gap-2">
                          {/* Telefone fixo não recebe mensagem; o link copiado
                              serve para qualquer outro meio. */}
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

        <button
          type="button"
          onClick={onClose}
          className="mt-5 rounded border border-line px-4 py-2 text-sm"
        >
          Fechar
        </button>
      </div>
    </div>
  )
}
