import { money } from './publicApi'

/** Números guardados como "(87) 99999-1234" viram "5587999991234". O DDI é
 *  acrescentado quando falta: o cadastro pede telefone brasileiro, e o wa.me
 *  recusa o número sem ele. */
export function toWhatsAppNumber(phone: string): string {
  const digits = phone.replace(/\D/g, '')
  return digits.startsWith('55') ? digits : `55${digits}`
}

export function publicQuoteUrl(token: string): string {
  return `${window.location.origin}/q/${token}`
}

export function quoteMessage(quote: {
  customerName: string
  serviceOrderNumber: number
  vehicleDescription: string
  vehiclePlate: string
  totalAmount: number
  publicToken: string
}): string {
  const firstName = quote.customerName.trim().split(/\s+/)[0]

  return [
    `Olá, ${firstName}! Aqui é da oficina.`,
    `O orçamento da OS-${quote.serviceOrderNumber} (${quote.vehicleDescription} · ${quote.vehiclePlate}) ficou em ${money(quote.totalAmount)}.`,
    `Você pode aprovar ou recusar por este link: ${publicQuoteUrl(quote.publicToken)}`,
    'O link vale por 7 dias.',
  ].join('\n\n')
}

/** D-15: sem API oficial do WhatsApp no MVP. O botão abre a conversa com a
 *  mensagem pronta e quem confirma o envio é o atendente. */
export function whatsAppLink(quote: Parameters<typeof quoteMessage>[0] & { customerPhone: string }): string {
  return `https://wa.me/${toWhatsAppNumber(quote.customerPhone)}?text=${encodeURIComponent(quoteMessage(quote))}`
}

/** Só celular tem WhatsApp: 11 dígitos, com o nono dígito na frente do número.
 *  Um cliente de telefone fixo continua cadastrado — o que muda é que o link
 *  do orçamento vai copiado, e não por mensagem. Espelha PhoneNumber.IsMobile
 *  do backend. */
export function hasWhatsApp(phone: string): boolean {
  const digits = phone.replace(/\D/g, '')
  return digits.length === 11 && digits[2] === '9'
}

/** O link é montado com a origem de onde a página foi aberta, então em produção
 *  ele já sai com o endereço do Render (D-26: um serviço só serve a API e a
 *  SPA). Rodando local, sai com o endereço da máquina — que não abre em lugar
 *  nenhum além dela. Isto existe para ninguém mandar um link morto ao cliente
 *  durante uma demonstração. */
export function isLocalOrigin(): boolean {
  const { hostname } = window.location

  return (
    hostname === 'localhost' ||
    hostname === '127.0.0.1' ||
    hostname === '[::1]' ||
    /^192\.168\./.test(hostname) ||
    /^10\./.test(hostname) ||
    /^172\.(1[6-9]|2\d|3[01])\./.test(hostname)
  )
}
