import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../api/client'
import type { components } from '../api/schema'

export type OrderPhoto = components['schemas']['ServiceOrderMediaResponse']

const photosKey = (orderId: string) => ['service-orders', orderId, 'media'] as const

function messageFrom(error: unknown, fallback: string): string {
  const problem = error as { title?: string; detail?: string } | undefined
  return problem?.detail ?? problem?.title ?? fallback
}

/** Um retrato de celular sai com 4000 px de largura e vários MB. O laudo não
 *  precisa disso, e a D-25 pede a compressão no cliente justamente porque o
 *  upload acontece no pátio, com o sinal que houver. */
const maxDimension = 1600
const quality = 0.8

export async function compress(file: File): Promise<File> {
  let bitmap: ImageBitmap

  try {
    bitmap = await createImageBitmap(file)
  } catch {
    // Formato que o navegador não decodifica — HEIC de iPhone, por exemplo.
    // Segue o original: quem recusa com mensagem clara é o servidor.
    return file
  }

  const scale = Math.min(1, maxDimension / Math.max(bitmap.width, bitmap.height))

  const canvas = document.createElement('canvas')
  canvas.width = Math.round(bitmap.width * scale)
  canvas.height = Math.round(bitmap.height * scale)

  const context = canvas.getContext('2d')
  if (!context) {
    return file
  }

  context.drawImage(bitmap, 0, 0, canvas.width, canvas.height)
  bitmap.close()

  const blob = await new Promise<Blob | null>((resolve) =>
    canvas.toBlob(resolve, 'image/jpeg', quality),
  )

  // Comprimir e sair maior acontece com imagem pequena que já veio otimizada.
  if (!blob || blob.size >= file.size) {
    return file
  }

  return new File([blob], file.name.replace(/\.[^.]+$/, '') + '.jpg', { type: 'image/jpeg' })
}

/** O endereço da foto. Passa pela API de propósito: o destino é detalhe de
 *  implementação (D-25) e o bucket de produção é privado (D-26). Como front e
 *  API dividem origem, o cookie vai junto no pedido da <img>. */
export function photoUrl(photo: OrderPhoto): string {
  return `/api/service-orders/${photo.serviceOrderId}/media/${photo.id}`
}

export function useOrderPhotos(orderId: string) {
  return useQuery({
    queryKey: photosKey(orderId),
    queryFn: async () => {
      const { data, error } = await api.GET('/api/service-orders/{orderId}/media', {
        params: { path: { orderId } },
      })

      if (error || !data) {
        throw new Error(messageFrom(error, 'Não foi possível carregar as fotos.'))
      }

      return data
    },
  })
}

export function useUploadPhoto(orderId: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async ({ file, caption }: { file: File; caption?: string }) => {
      const photo = await compress(file)

      const { data, error } = await api.POST('/api/service-orders/{orderId}/media', {
        params: { path: { orderId } },
        // O contrato descreve o arquivo como binário, que o gerador escreve
        // como string; o que vai no corpo é o File mesmo.
        body: { File: photo as unknown as string, Caption: caption },
        // O corpo é opcional no tipo gerado, porque o Swagger não exige corpo
        // em todo POST. Aqui ele sempre vem — a guarda existe para o TypeScript.
        bodySerializer(body) {
          const form = new FormData()

          if (body) {
            form.append('File', body.File as unknown as Blob)

            if (body.Caption) {
              form.append('Caption', body.Caption)
            }
          }

          return form
        },
      })

      if (error || !data) {
        throw new Error(messageFrom(error, 'Não foi possível enviar a foto.'))
      }

      return data
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: photosKey(orderId) }),
  })
}

export function useRemovePhoto(orderId: string) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (mediaId: string) => {
      const { error } = await api.DELETE('/api/service-orders/{orderId}/media/{mediaId}', {
        params: { path: { orderId, mediaId } },
      })

      if (error) {
        throw new Error(messageFrom(error, 'Não foi possível remover a foto.'))
      }
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: photosKey(orderId) }),
  })
}
