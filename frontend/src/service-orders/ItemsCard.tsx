import { useState } from 'react'
import { formatQuantity, usePartOptions } from '../inventory/api'
import { formatMoney } from './board'
import { useRemoveItem, useSaveItem, type ItemInput, type ServiceOrderItem } from './api'

const empty: ItemInput = {
  itemType: 'SERVICE',
  partId: null,
  description: '',
  quantity: 1,
  unitPrice: 0,
}

/** Peça exige `partId` e serviço exige que ele seja nulo — a constraint
 *  ck_item_part_required cobra isso no banco, e é o PART que a baixa da D-11
 *  procura ao concluir a OS. Serviço continua com descrição livre. */
function ItemForm({
  orderId,
  editing,
  onDone,
}: {
  orderId: string
  editing: ServiceOrderItem | null
  onDone: () => void
}) {
  const save = useSaveItem(orderId)
  const { data: parts } = usePartOptions()
  const [form, setForm] = useState<ItemInput>(
    editing
      ? {
          itemType: editing.itemType,
          partId: editing.partId,
          description: editing.description,
          quantity: editing.quantity,
          unitPrice: editing.unitPrice,
        }
      : empty,
  )

  /** Trocar o tipo limpa a peça: serviço com partId preenchido é recusado pelo
   *  banco. O que já foi digitado fica, porque costuma servir nos dois. */
  function chooseType(itemType: ItemInput['itemType']) {
    setForm({ ...form, itemType, partId: null })
  }

  /** O cadastro preenche descrição e preço, mas os dois seguem editáveis: o
   *  que for enviado é o que a OS mostra para sempre (D-07). */
  function choosePart(partId: string) {
    const part = parts?.find((option) => option.id === partId)

    setForm({
      ...form,
      partId: partId || null,
      description: part ? part.name : form.description,
      unitPrice: part ? part.salePrice : form.unitPrice,
    })
  }

  function submit(event: React.FormEvent) {
    event.preventDefault()
    save.mutate({ ...form, itemId: editing?.id }, { onSuccess: onDone })
  }

  return (
    <form onSubmit={submit} className="mt-3 rounded border border-line bg-surface p-3">
      <fieldset className="flex gap-4">
        <legend className="sr-only">Tipo do item</legend>
        {(['SERVICE', 'PART'] as const).map((type) => (
          <label key={type} className="flex items-center gap-1.5 text-sm">
            <input
              type="radio"
              name="itemType"
              checked={form.itemType === type}
              onChange={() => chooseType(type)}
            />
            {type === 'SERVICE' ? 'Serviço' : 'Peça'}
          </label>
        ))}
      </fieldset>

      {form.itemType === 'PART' && (
        <label className="mt-3 block">
          <span className="text-xs font-medium">Peça do estoque</span>
          <select
            required
            value={form.partId ?? ''}
            onChange={(event) => choosePart(event.target.value)}
            className="mt-1 w-full rounded border border-line bg-panel px-2 py-1 text-sm"
          >
            <option value="">Selecione a peça</option>
            {parts?.map((part) => (
              <option key={part.id} value={part.id}>
                {part.sku} · {part.name} ({formatQuantity(part.quantityOnHand)} {part.unit})
              </option>
            ))}
          </select>
          {/* D-11 e D-41: a baixa acontece ao concluir a OS, e o saldo pode
              ficar negativo — por isso lançar mais do que há não é recusado. */}
          <span className="mt-1 block text-xs text-ink-soft">
            A baixa do estoque acontece quando a OS for concluída.
          </span>
        </label>
      )}

      <div className="mt-3 grid gap-3 sm:grid-cols-[1fr_6rem_8rem]">
        <label className="block">
          <span className="text-xs font-medium">Descrição</span>
          <input
            required
            minLength={2}
            maxLength={200}
            value={form.description}
            onChange={(event) => setForm({ ...form, description: event.target.value })}
            placeholder="Troca de óleo e filtro"
            className="mt-1 w-full rounded border border-line px-2 py-1 text-sm"
          />
        </label>

        <label className="block">
          <span className="text-xs font-medium">Qtd.</span>
          <input
            type="number"
            required
            min={0.001}
            step={0.001}
            value={form.quantity}
            onChange={(event) => setForm({ ...form, quantity: Number(event.target.value) })}
            className="mt-1 w-full rounded border border-line px-2 py-1 text-sm"
          />
        </label>

        <label className="block">
          <span className="text-xs font-medium">Preço unitário</span>
          <input
            type="number"
            required
            min={0}
            step={0.01}
            value={form.unitPrice}
            onChange={(event) => setForm({ ...form, unitPrice: Number(event.target.value) })}
            className="mt-1 w-full rounded border border-line px-2 py-1 text-sm"
          />
        </label>
      </div>

      {save.isError && (
        <p role="alert" className="mt-2 text-sm">
          {(save.error as Error).message}
        </p>
      )}

      <div className="mt-3 flex gap-2">
        <button
          type="submit"
          disabled={save.isPending}
          className="rounded bg-brand px-3 py-1 text-xs font-medium text-white disabled:opacity-50"
        >
          {save.isPending ? 'Salvando…' : editing ? 'Salvar item' : 'Lançar item'}
        </button>
        <button
          type="button"
          onClick={onDone}
          className="rounded border border-line px-3 py-1 text-xs"
        >
          Cancelar
        </button>
      </div>
    </form>
  )
}

export function ItemsCard({
  orderId,
  items,
  itemsTotal,
  discount,
  total,
  editable,
}: {
  orderId: string
  items: ServiceOrderItem[]
  itemsTotal: number
  discount: number
  total: number
  editable: boolean
}) {
  const [adding, setAdding] = useState(false)
  const [editing, setEditing] = useState<ServiceOrderItem | null>(null)
  const remove = useRemoveItem(orderId)

  return (
    <div className="rounded-lg border border-line bg-panel p-4">
      <div className="flex items-center justify-between">
        <h2 className="text-sm font-semibold">Serviços e peças</h2>
        {editable && !adding && !editing && (
          <button
            type="button"
            onClick={() => setAdding(true)}
            className="rounded bg-brand px-3 py-1 text-xs font-medium text-white"
          >
            Lançar item
          </button>
        )}
      </div>

      {items.length === 0 ? (
        <p className="mt-3 text-sm text-ink-soft">
          Nenhum item lançado. O orçamento precisa de ao menos um.
        </p>
      ) : (
        <ul className="mt-3 divide-y divide-line">
          {items.map((item) => (
            <li key={item.id} className="flex flex-wrap items-center gap-2 py-2 text-sm">
              <span className="grow">
                {item.description}
                <span className="text-ink-soft">
                  {' '}
                  · {item.quantity} × {formatMoney(item.unitPrice)}
                </span>
              </span>
              <span className="font-medium">{formatMoney(item.total)}</span>
              {editable && (
                <span className="flex gap-2">
                  <button
                    type="button"
                    onClick={() => setEditing(item)}
                    className="rounded border border-line px-2 py-0.5 text-xs"
                  >
                    Editar
                  </button>
                  <button
                    type="button"
                    onClick={() => remove.mutate(item.id)}
                    className="rounded border border-line px-2 py-0.5 text-xs"
                  >
                    Remover
                  </button>
                </span>
              )}
            </li>
          ))}
        </ul>
      )}

      {remove.isError && (
        <p role="alert" className="mt-2 text-sm">
          {(remove.error as Error).message}
        </p>
      )}

      {editable && (adding || editing) && (
        <ItemForm
          key={editing?.id ?? 'new'}
          orderId={orderId}
          editing={editing}
          onDone={() => {
            setAdding(false)
            setEditing(null)
          }}
        />
      )}

      {/* D-09: o total é calculado a partir dos itens, nunca guardado. */}
      <dl className="mt-4 space-y-1 border-t border-line pt-3 text-sm">
        <div className="flex justify-between text-ink-soft">
          <dt>Itens</dt>
          <dd>{formatMoney(itemsTotal)}</dd>
        </div>
        {discount > 0 && (
          <div className="flex justify-between text-ink-soft">
            <dt>Desconto</dt>
            <dd>-{formatMoney(discount)}</dd>
          </div>
        )}
        <div className="flex justify-between font-semibold">
          <dt>Total</dt>
          <dd>{formatMoney(total)}</dd>
        </div>
      </dl>

      {!editable && (
        <p className="mt-3 text-xs text-ink-soft">
          A OS não aceita mais mudança de itens nesta situação.
        </p>
      )}
    </div>
  )
}
