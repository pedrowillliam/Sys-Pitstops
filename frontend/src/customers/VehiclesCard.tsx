import { useState } from 'react'
import {
  describeVehicle,
  formatPlate,
  useRemoveVehicle,
  useSaveVehicle,
  useVehicles,
  type Vehicle,
  type VehicleInput,
} from './vehicles'

/** O que o formulário do protótipo pede em "Vincular Veículo". O dono entra na
 *  hora de gravar, porque num cliente novo ele ainda não existe. */
export type DraftVehicle = Omit<VehicleInput, 'ownerId'>

const emptyVehicle: DraftVehicle = {
  plate: '',
  brand: '',
  model: '',
  modelYear: null,
  color: null,
  vin: null,
}

function VehicleFields({
  value,
  onChange,
}: {
  value: DraftVehicle
  onChange: (next: DraftVehicle) => void
}) {
  return (
    <div className="grid gap-3 sm:grid-cols-2">
      <label className="block">
        <span className="text-xs font-medium">
          Placa<span aria-hidden="true"> *</span>
        </span>
        <input
          required
          value={value.plate}
          onChange={(event) => onChange({ ...value, plate: formatPlate(event.target.value) })}
          placeholder="ABC1D23"
          className="mt-1 w-full rounded border border-line px-2 py-1.5 font-mono text-sm uppercase"
        />
      </label>

      <label className="block">
        <span className="text-xs font-medium">Cor</span>
        <input
          maxLength={30}
          value={value.color ?? ''}
          onChange={(event) => onChange({ ...value, color: event.target.value || null })}
          className="mt-1 w-full rounded border border-line px-2 py-1.5 text-sm"
        />
      </label>

      <label className="block">
        <span className="text-xs font-medium">
          Marca<span aria-hidden="true"> *</span>
        </span>
        <input
          required
          maxLength={60}
          value={value.brand}
          onChange={(event) => onChange({ ...value, brand: event.target.value })}
          placeholder="Fiat"
          className="mt-1 w-full rounded border border-line px-2 py-1.5 text-sm"
        />
      </label>

      <label className="block">
        <span className="text-xs font-medium">
          Modelo<span aria-hidden="true"> *</span>
        </span>
        <input
          required
          maxLength={60}
          value={value.model}
          onChange={(event) => onChange({ ...value, model: event.target.value })}
          placeholder="Strada"
          className="mt-1 w-full rounded border border-line px-2 py-1.5 text-sm"
        />
      </label>

      <label className="block">
        <span className="text-xs font-medium">Ano</span>
        <input
          type="number"
          min={1900}
          max={2100}
          value={value.modelYear ?? ''}
          onChange={(event) =>
            onChange({
              ...value,
              modelYear: event.target.value ? Number(event.target.value) : null,
            })
          }
          className="mt-1 w-full rounded border border-line px-2 py-1.5 text-sm"
        />
      </label>

      <label className="block">
        <span className="text-xs font-medium">Chassi</span>
        <input
          maxLength={17}
          value={value.vin ?? ''}
          onChange={(event) => onChange({ ...value, vin: event.target.value || null })}
          className="mt-1 w-full rounded border border-line px-2 py-1.5 font-mono text-sm"
        />
      </label>
    </div>
  )
}

function Table({
  vehicles,
  onEdit,
  onRemove,
}: {
  vehicles: { id?: string; plate: string; brand: string; model: string; modelYear?: number | null; color?: string | null }[]
  onEdit?: (index: number) => void
  onRemove: (index: number) => void
}) {
  return (
    <div className="mt-3 overflow-x-auto">
      <table className="w-full min-w-[30rem] text-left text-sm">
        <thead className="text-xs uppercase text-ink-soft">
          <tr>
            <th className="pb-2 pr-3 font-medium">Placa</th>
            <th className="pb-2 pr-3 font-medium">Modelo</th>
            <th className="pb-2 pr-3 font-medium">Ano</th>
            <th className="pb-2 pr-3 font-medium">Cor</th>
            <th className="pb-2 font-medium" />
          </tr>
        </thead>
        <tbody>
          {vehicles.map((vehicle, index) => (
            <tr key={vehicle.id ?? `${vehicle.plate}-${index}`} className="border-t border-line">
              <td className="py-2 pr-3 font-mono text-xs">{vehicle.plate}</td>
              <td className="py-2 pr-3">{describeVehicle(vehicle)}</td>
              <td className="py-2 pr-3 text-ink-soft">{vehicle.modelYear ?? '—'}</td>
              <td className="py-2 pr-3 text-ink-soft">{vehicle.color ?? '—'}</td>
              <td className="py-2 text-right">
                <span className="flex justify-end gap-2">
                  {onEdit && (
                    <button
                      type="button"
                      onClick={() => onEdit(index)}
                      className="rounded border border-line px-2 py-0.5 text-xs"
                    >
                      Editar
                    </button>
                  )}
                  <button
                    type="button"
                    onClick={() => onRemove(index)}
                    className="rounded border border-line px-2 py-0.5 text-xs"
                  >
                    Remover
                  </button>
                </span>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

function Shell({ children, action }: { children: React.ReactNode; action: React.ReactNode }) {
  return (
    <section className="rounded border border-line bg-panel p-6">
      <div className="flex items-center justify-between">
        <h2 className="text-base font-semibold">Veículos vinculados</h2>
        {action}
      </div>
      {children}
    </section>
  )
}

/**
 * Cliente **novo**: os veículos ficam em memória e são gravados junto com o
 * "Salvar", como o protótipo desenha. Não dá para gravá-los antes — o veículo
 * exige um dono que ainda não existe.
 */
export function DraftVehicles({
  vehicles,
  onChange,
}: {
  vehicles: DraftVehicle[]
  onChange: (next: DraftVehicle[]) => void
}) {
  const [draft, setDraft] = useState<DraftVehicle | null>(null)

  function add() {
    if (!draft?.plate.trim() || !draft.brand.trim() || !draft.model.trim()) {
      return
    }

    onChange([...vehicles, draft])
    setDraft(null)
  }

  return (
    <Shell
      action={
        !draft && (
          <button
            type="button"
            onClick={() => setDraft(emptyVehicle)}
            className="rounded bg-brand px-3 py-1.5 text-xs font-medium text-white"
          >
            Vincular veículo
          </button>
        )
      }
    >
      {vehicles.length === 0 && !draft ? (
        <p className="mt-3 text-sm text-ink-soft">
          Nenhum veículo. Sem pelo menos um, este cliente não poderá receber ordem de
          serviço.
        </p>
      ) : (
        vehicles.length > 0 && (
          <Table
            vehicles={vehicles}
            onRemove={(index) => onChange(vehicles.filter((_, i) => i !== index))}
          />
        )
      )}

      {draft && (
        // Não é <form>: este bloco vive dentro do formulário do cliente, e um
        // form aninhado não é HTML válido — o Enter submeteria o de fora.
        <div className="mt-3 rounded-lg border border-line bg-surface p-3">
          <VehicleFields value={draft} onChange={setDraft} />

          <div className="mt-3 flex gap-2">
            <button
              type="button"
              onClick={add}
              className="rounded bg-brand px-3 py-1.5 text-xs font-medium text-white"
            >
              Adicionar à lista
            </button>
            <button
              type="button"
              onClick={() => setDraft(null)}
              className="rounded border border-line px-3 py-1.5 text-xs"
            >
              Cancelar
            </button>
          </div>
        </div>
      )}
    </Shell>
  )
}

/** Cliente **existente**: cada alteração vai direto para a API, porque já há
 *  dono e não faz sentido segurar o carro esperando um "salvar" do cadastro. */
export function VehiclesCard({ ownerId }: { ownerId: string }) {
  const { data: vehicles, isPending } = useVehicles(ownerId)
  const save = useSaveVehicle(ownerId)
  const remove = useRemoveVehicle(ownerId)
  const [draft, setDraft] = useState<DraftVehicle | null>(null)
  const [editing, setEditing] = useState<Vehicle | null>(null)

  const open = draft !== null || editing !== null
  const value = draft ?? (editing ? { ...editing } : null)

  function submit() {
    if (!value) {
      return
    }

    save.mutate(
      { ...value, ownerId, id: editing?.id },
      {
        onSuccess: () => {
          setDraft(null)
          setEditing(null)
        },
      },
    )
  }

  return (
    <div className="mt-6">
      <Shell
        action={
          !open && (
            <button
              type="button"
              onClick={() => setDraft(emptyVehicle)}
              className="rounded bg-brand px-3 py-1.5 text-xs font-medium text-white"
            >
              Vincular veículo
            </button>
          )
        }
      >
        {isPending ? (
          <p className="mt-3 text-sm text-ink-soft">Carregando…</p>
        ) : vehicles && vehicles.length === 0 ? (
          <p className="mt-3 text-sm text-ink-soft">
            Nenhum veículo. Sem pelo menos um, este cliente não poderá receber ordem de
            serviço.
          </p>
        ) : (
          <Table
            vehicles={vehicles ?? []}
            onEdit={(index) => setEditing((vehicles ?? [])[index])}
            onRemove={(index) => remove.mutate((vehicles ?? [])[index].id)}
          />
        )}

        {/* O servidor recusa remover veículo que já tem OS, para não apagar
            histórico de atendimento. A mensagem vem de lá. */}
        {(remove.isError || save.isError) && (
          <p role="alert" className="mt-2 text-sm">
            {((remove.error ?? save.error) as Error).message}
          </p>
        )}

        {open && value && (
          <div className="mt-3 rounded-lg border border-line bg-surface p-3">
            <VehicleFields
              value={value}
              onChange={(next) => (editing ? setEditing({ ...editing, ...next }) : setDraft(next))}
            />

            <div className="mt-3 flex gap-2">
              <button
                type="button"
                disabled={save.isPending}
                onClick={submit}
                className="rounded bg-brand px-3 py-1.5 text-xs font-medium text-white disabled:opacity-50"
              >
                {save.isPending ? 'Salvando…' : editing ? 'Salvar veículo' : 'Adicionar veículo'}
              </button>
              <button
                type="button"
                onClick={() => {
                  setDraft(null)
                  setEditing(null)
                }}
                className="rounded border border-line px-3 py-1.5 text-xs"
              >
                Cancelar
              </button>
            </div>
          </div>
        )}
      </Shell>
    </div>
  )
}
