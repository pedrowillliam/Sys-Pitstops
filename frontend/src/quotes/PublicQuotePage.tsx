import { useState } from 'react'
import { useParams } from 'react-router'
import { money, useAnswerQuote, usePublicQuote, type QuoteItem } from './publicApi'

function Rows({ title, items }: { title: string; items: QuoteItem[] }) {
  if (items.length === 0) {
    return null
  }

  return (
    <>
      <tr>
        <th colSpan={4} className="pt-5 pb-2 text-left text-sm font-semibold">
          {title}
        </th>
      </tr>
      {items.map((item, index) => (
        <tr key={`${item.description}-${index}`} className="border-b border-line">
          <td className="py-2.5 text-sm">{item.description}</td>
          <td className="py-2.5 text-right text-sm text-ink-soft">{item.quantity}</td>
          <td className="py-2.5 text-right text-sm text-ink-soft">{money(item.unitPrice)}</td>
          <td className="py-2.5 text-right text-sm font-medium">{money(item.total)}</td>
        </tr>
      ))}
    </>
  )
}

function Answered({ approved }: { approved: boolean }) {
  return (
    <div
      className={`rounded-lg border p-5 text-center ${
        approved ? 'border-emerald-300 bg-emerald-50' : 'border-line bg-surface'
      }`}
    >
      <p className="font-semibold">
        {approved ? 'Orçamento aprovado. Obrigado!' : 'Orçamento recusado.'}
      </p>
      <p className="mt-1 text-sm text-ink-soft">
        {approved
          ? 'A oficina já foi avisada e o serviço entrou em execução.'
          : 'A oficina foi avisada e pode entrar em contato com outra proposta.'}
      </p>
    </div>
  )
}

/**
 * The one screen a stranger can open. No login, no menu, no navigation back
 * into the system: the customer arrives from a WhatsApp link (D-15), reads the
 * quote and answers it.
 */
export function PublicQuotePage() {
  const { token = '' } = useParams()
  const { data: quote, isPending, isError, error } = usePublicQuote(token)
  const answer = useAnswerQuote(token)
  const [rejecting, setRejecting] = useState(false)
  const [reason, setReason] = useState('')

  if (isPending) {
    return <p className="p-8 text-center text-sm text-ink-soft">Carregando orçamento…</p>
  }

  // A missing link and an expired one look the same on purpose.
  if (isError || !quote) {
    return (
      <main className="mx-auto max-w-md p-6">
        <div className="rounded-lg border border-line bg-panel p-6 text-center">
          <h1 className="text-lg font-semibold">Orçamento não encontrado</h1>
          <p className="mt-2 text-sm text-ink-soft">
            {isError
              ? (error as Error).message
              : 'O link pode ter expirado. Peça um novo à oficina.'}
          </p>
        </div>
      </main>
    )
  }

  const parts = quote.items.filter((i) => i.itemType === 'PART')
  const services = quote.items.filter((i) => i.itemType === 'SERVICE')
  const partsTotal = parts.reduce((sum, i) => sum + i.total, 0)
  const servicesTotal = services.reduce((sum, i) => sum + i.total, 0)
  const open = quote.status === 'SENT'

  return (
    <main className="mx-auto max-w-3xl p-4 md:p-8">
      <header className="text-center">
        <h1 className="text-xl font-semibold">{quote.workshopName}</h1>
        <p className="text-sm text-ink-soft">Orçamento nº {quote.serviceOrderNumber}</p>
      </header>

      {open && (
        <p className="mt-6 rounded-lg border border-line bg-panel p-4 text-sm">
          Olá, {quote.customerName}. Avalie abaixo as peças e serviços do seu veículo.
          Ao aprovar, a oficina inicia a execução imediatamente.
        </p>
      )}

      <section className="mt-4 grid gap-3 sm:grid-cols-3">
        <div className="rounded-lg border border-line bg-panel p-4">
          <p className="text-xs uppercase text-ink-soft">Veículo</p>
          <p className="mt-1 font-semibold">{quote.vehicleDescription}</p>
          <p className="mt-1 inline-block rounded border border-line px-2 py-0.5 font-mono text-xs">
            {quote.vehiclePlate}
          </p>
        </div>
        <div className="rounded-lg border border-line bg-panel p-4">
          <p className="text-xs uppercase text-ink-soft">Cliente</p>
          <p className="mt-1 font-semibold">{quote.customerName}</p>
        </div>
        <div className="rounded-lg border border-line bg-panel p-4">
          <p className="text-xs uppercase text-ink-soft">Mecânico responsável</p>
          <p className="mt-1 font-semibold">{quote.mechanicName ?? 'A definir'}</p>
        </div>
      </section>

      <section className="mt-4 overflow-x-auto rounded-lg border border-line bg-panel p-4 md:p-6">
        <table className="w-full">
          <thead>
            <tr className="border-b border-line text-xs uppercase text-ink-soft">
              <th className="pb-2 text-left font-medium">Item</th>
              <th className="pb-2 text-right font-medium">Qtd.</th>
              <th className="pb-2 text-right font-medium">Valor unit.</th>
              <th className="pb-2 text-right font-medium">Subtotal</th>
            </tr>
          </thead>
          <tbody>
            <Rows title="Peças" items={parts} />
            <Rows title="Serviços" items={services} />
          </tbody>
        </table>

        <div className="mt-5 space-y-1 border-t border-line pt-4 text-sm">
          {parts.length > 0 && (
            <div className="flex justify-between text-ink-soft">
              <span>Subtotal peças</span>
              <span>{money(partsTotal)}</span>
            </div>
          )}
          {services.length > 0 && (
            <div className="flex justify-between text-ink-soft">
              <span>Subtotal serviços</span>
              <span>{money(servicesTotal)}</span>
            </div>
          )}
          <div className="flex items-center justify-between pt-2 text-lg font-semibold">
            <span>Total</span>
            <span>{money(quote.totalAmount)}</span>
          </div>
        </div>
      </section>

      <section className="mt-4">
        {!open ? (
          <Answered approved={quote.status === 'APPROVED'} />
        ) : (
          <div className="rounded-lg border border-line bg-panel p-5">
            <h2 className="font-semibold">Sua decisão</h2>
            <p className="mt-1 text-sm text-ink-soft">
              Escolha uma opção para prosseguirmos com a manutenção do veículo.
            </p>

            {answer.isError && (
              <p role="alert" className="mt-3 text-sm">
                {(answer.error as Error).message}
              </p>
            )}

            {rejecting && (
              <label className="mt-4 block">
                <span className="text-sm font-medium">Motivo (opcional)</span>
                <textarea
                  rows={3}
                  value={reason}
                  onChange={(event) => setReason(event.target.value)}
                  className="mt-1 w-full rounded border border-line px-3 py-2 text-sm"
                />
              </label>
            )}

            <div className="mt-4 flex flex-col gap-3 sm:flex-row">
              <button
                type="button"
                disabled={answer.isPending}
                onClick={() => answer.mutate({ approve: true })}
                className="flex-1 rounded-lg bg-emerald-600 px-4 py-3 font-medium text-white disabled:opacity-50"
              >
                {answer.isPending ? 'Enviando…' : 'Aprovar orçamento'}
              </button>

              <button
                type="button"
                disabled={answer.isPending}
                onClick={() =>
                  rejecting ? answer.mutate({ approve: false, reason }) : setRejecting(true)
                }
                className="flex-1 rounded-lg border border-line px-4 py-3 font-medium disabled:opacity-50"
              >
                {rejecting ? 'Confirmar recusa' : 'Recusar'}
              </button>
            </div>

            <p className="mt-4 text-center text-xs text-ink-soft">
              Orçamento válido até{' '}
              {new Date(quote.expiresAt).toLocaleDateString('pt-BR')}.
            </p>
          </div>
        )}
      </section>

      {/* O laudo fotográfico do protótipo depende do upload de fotos, que entra
          junto com o IMediaStorage da D-25. */}
    </main>
  )
}
