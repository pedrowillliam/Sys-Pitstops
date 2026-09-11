import { useRef, useState } from 'react'
import { photoUrl, useOrderPhotos, useRemovePhoto, useUploadPhoto } from './photos'

/** O "Registro Fotográfico (Avarias/Peças)" do protótipo: as miniaturas em
 *  linha e, ao lado, Capturar e Adicionar. Capturar pede a câmera traseira pelo
 *  atributo `capture`, que é o que o celular do pátio entende; Adicionar abre a
 *  galeria. Fora do celular os dois caem no mesmo seletor de arquivo. */
export function PhotosCard({ orderId, editable }: { orderId: string; editable: boolean }) {
  const { data: photos, isPending } = useOrderPhotos(orderId)
  const upload = useUploadPhoto(orderId)
  const remove = useRemovePhoto(orderId)
  const [open, setOpen] = useState<string | null>(null)

  const camera = useRef<HTMLInputElement>(null)
  const gallery = useRef<HTMLInputElement>(null)

  function send(input: HTMLInputElement) {
    const file = input.files?.[0]

    if (file) {
      upload.mutate({ file })
    }

    // Zera para que escolher a mesma foto de novo dispare o evento.
    input.value = ''
  }

  return (
    <div>
      <h3 className="text-sm font-medium">Registro fotográfico (avarias/peças)</h3>

      {isPending ? (
        <p className="mt-2 text-sm text-ink-soft">Carregando fotos…</p>
      ) : (
        <div className="mt-2 flex flex-wrap gap-2">
          {photos?.map((photo) => (
            <figure key={photo.id} className="relative">
              <button
                type="button"
                onClick={() => setOpen(photoUrl(photo))}
                className="block size-20 overflow-hidden rounded-lg border border-line"
              >
                <img
                  src={photoUrl(photo)}
                  alt={photo.caption ?? `Foto de ${photo.uploadedByName}`}
                  loading="lazy"
                  className="size-full object-cover"
                />
              </button>

              {editable && (
                <button
                  type="button"
                  aria-label="Remover foto"
                  onClick={() => remove.mutate(photo.id)}
                  className="absolute -right-1.5 -top-1.5 flex size-5 items-center justify-center rounded-full border border-line bg-panel text-xs leading-none"
                >
                  ×
                </button>
              )}
            </figure>
          ))}

          {editable && (
            <>
              <input
                ref={camera}
                type="file"
                accept="image/*"
                capture="environment"
                onChange={(event) => send(event.currentTarget)}
                className="hidden"
              />
              <input
                ref={gallery}
                type="file"
                accept="image/jpeg,image/png,image/webp"
                onChange={(event) => send(event.currentTarget)}
                className="hidden"
              />

              <button
                type="button"
                disabled={upload.isPending}
                onClick={() => camera.current?.click()}
                className="flex size-20 flex-col items-center justify-center gap-1 rounded-lg border border-dashed border-sky-400 text-xs text-sky-700 disabled:opacity-50"
              >
                <span aria-hidden="true" className="text-base">
                  📷
                </span>
                {upload.isPending ? 'Enviando…' : 'Capturar'}
              </button>

              <button
                type="button"
                disabled={upload.isPending}
                onClick={() => gallery.current?.click()}
                className="flex size-20 flex-col items-center justify-center gap-1 rounded-lg border border-line bg-surface text-xs text-ink-soft disabled:opacity-50"
              >
                <span aria-hidden="true" className="text-base">
                  +
                </span>
                Adicionar
              </button>
            </>
          )}
        </div>
      )}

      {photos?.length === 0 && !editable && (
        <p className="mt-2 text-sm text-ink-soft">Nenhuma foto registrada.</p>
      )}

      {(upload.isError || remove.isError) && (
        <p role="alert" className="mt-2 text-sm">
          {((upload.error ?? remove.error) as Error).message}
        </p>
      )}

      {open && (
        <button
          type="button"
          aria-label="Fechar foto"
          onClick={() => setOpen(null)}
          className="fixed inset-0 z-20 flex items-center justify-center bg-black/80 p-4"
        >
          <img src={open} alt="" className="max-h-full max-w-full rounded" />
        </button>
      )}
    </div>
  )
}
