import { useState } from 'react'
import { Link, useNavigate } from 'react-router'
import { useCustomers } from '../customers/api'
import { useMechanics, useOpenOrder } from './api'

/** A OS congela o cliente pelo dono do veículo (D-08), então o cliente aqui é
 *  filtro de tela: ele escolhe de qual lista de veículos sair. */
export function OpenOrderPage() {
  const navigate = useNavigate()
  const open = useOpenOrder()

  const { data: customers, isPending } = useCustomers({
    search: '',
    includeInactive: false,
    page: 1,
  })
  const { data: mechanics } = useMechanics()

  const [customerId, setCustomerId] = useState('')
  const [vehicleId, setVehicleId] = useState('')
  const [mechanicId, setMechanicId] = useState('')
  const [scheduledAt, setScheduledAt] = useState('')
  const [mileage, setMileage] = useState('')
  const [reportedIssue, setReportedIssue] = useState('')

  const customer = customers?.items.find((c) => c.id === customerId)
  const vehicles = customer?.vehicles ?? []

  function chooseCustomer(id: string) {
    setCustomerId(id)
    setVehicleId('')
  }

  function submit(event: React.FormEvent) {
    event.preventDefault()

    open.mutate(
      {
        vehicleId,
        mechanicId: mechanicId || null,
        mileage: mileage ? Number(mileage) : null,
        reportedIssue: reportedIssue.trim() || null,
        // O campo é uma data; o backend guarda timestamptz em UTC.
        scheduledAt: scheduledAt ? new Date(`${scheduledAt}T12:00`).toISOString() : null,
      },
      { onSuccess: (order) => navigate(`/service-orders/${order.id}`) },
    )
  }

  return (
    <section className="mx-auto max-w-3xl">
      <header className="rounded bg-brand px-4 py-3 text-sm font-medium text-white">
        Abertura de Ordem de Serviço
      </header>

      <form onSubmit={submit} className="mt-4 space-y-4 rounded-lg border border-line bg-panel p-6">
        <h2 className="text-sm font-semibold">Dados de abertura</h2>

        {isPending ? (
          <p className="text-sm text-ink-soft">Carregando clientes…</p>
        ) : (
          <div className="grid gap-4 sm:grid-cols-2">
            <label className="block">
              <span className="text-sm font-medium">
                Cliente<span aria-hidden="true"> *</span>
              </span>
              <select
                required
                value={customerId}
                onChange={(event) => chooseCustomer(event.target.value)}
                className="mt-1 w-full rounded border border-line bg-panel px-3 py-2 text-sm"
              >
                <option value="">Selecione um cliente cadastrado</option>
                {customers?.items.map((option) => (
                  <option key={option.id} value={option.id}>
                    {option.name}
                  </option>
                ))}
              </select>
            </label>

            <label className="block">
              <span className="text-sm font-medium">
                Veículo<span aria-hidden="true"> *</span>
              </span>
              <select
                required
                disabled={!customerId}
                value={vehicleId}
                onChange={(event) => setVehicleId(event.target.value)}
                className="mt-1 w-full rounded border border-line bg-panel px-3 py-2 text-sm disabled:opacity-50"
              >
                <option value="">
                  {customerId ? 'Selecione o veículo do cliente' : 'Escolha o cliente primeiro'}
                </option>
                {vehicles.map((vehicle) => (
                  <option key={vehicle.id} value={vehicle.id}>
                    {[vehicle.brand, vehicle.model, vehicle.modelYear].filter(Boolean).join(' ')} ·{' '}
                    {vehicle.plate}
                  </option>
                ))}
              </select>
              {/* Constatar o problema sem dar saída trava quem está abrindo a
                  OS: o veículo se cadastra na ficha do cliente. */}
              {customerId && vehicles.length === 0 && (
                <span className="mt-1 block text-xs text-ink-soft">
                  Este cliente não tem veículo cadastrado.{' '}
                  <Link to={`/customers/${customerId}`} className="font-medium underline">
                    Cadastrar agora
                  </Link>
                  .
                </span>
              )}
            </label>

            <label className="block">
              <span className="text-sm font-medium">Mecânico responsável</span>
              <select
                value={mechanicId}
                onChange={(event) => setMechanicId(event.target.value)}
                className="mt-1 w-full rounded border border-line bg-panel px-3 py-2 text-sm"
              >
                <option value="">Definir depois</option>
                {mechanics?.map((mechanic) => (
                  <option key={mechanic.id} value={mechanic.id}>
                    {mechanic.name}
                  </option>
                ))}
              </select>
              {/* D-38: o carro chega antes de alguém assumir, mas a partir de
                  "Em Análise" a ordem precisa de responsável. */}
              <span className="mt-1 block text-xs text-ink-soft">
                Opcional agora; exigido para mover a OS para Em Análise.
              </span>
            </label>

            <label className="block">
              <span className="text-sm font-medium">Previsão de entrega</span>
              <input
                type="date"
                value={scheduledAt}
                onChange={(event) => setScheduledAt(event.target.value)}
                className="mt-1 w-full rounded border border-line px-3 py-2 text-sm"
              />
            </label>

            <label className="block">
              <span className="text-sm font-medium">Quilometragem</span>
              <input
                type="number"
                min={0}
                value={mileage}
                onChange={(event) => setMileage(event.target.value)}
                className="mt-1 w-full rounded border border-line px-3 py-2 text-sm"
              />
            </label>
          </div>
        )}

        <label className="block">
          <span className="text-sm font-medium">Descrição do problema relatado</span>
          <textarea
            rows={3}
            value={reportedIssue}
            onChange={(event) => setReportedIssue(event.target.value)}
            placeholder="Descreva os sintomas ou problemas apontados pelo cliente…"
            className="mt-1 w-full rounded border border-line px-3 py-2 text-sm"
          />
        </label>

        {/* D-37: serviços e valor estimado não entram aqui. Serviço é item com
            preço congelado (D-07), e o total é calculado (D-09) — os dois são
            lançados na OS depois de aberta. */}
        <p className="rounded bg-surface px-3 py-2 text-xs text-ink-soft">
          Os serviços e peças são lançados na OS depois de aberta, com o preço de cada um.
        </p>

        {open.isError && (
          <p role="alert" className="text-sm">
            {(open.error as Error).message}
          </p>
        )}

        <div className="flex gap-3 pt-2">
          <button
            type="submit"
            disabled={!vehicleId || open.isPending}
            className="rounded bg-brand px-4 py-2 text-sm font-medium text-white disabled:opacity-50"
          >
            {open.isPending ? 'Abrindo…' : 'Abrir OS'}
          </button>
          <button
            type="button"
            onClick={() => navigate('/service-orders')}
            className="rounded border border-line px-4 py-2 text-sm"
          >
            Cancelar
          </button>
        </div>
      </form>
    </section>
  )
}
