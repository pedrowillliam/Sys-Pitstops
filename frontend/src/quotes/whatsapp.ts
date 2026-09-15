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

/**
 * A mensagem que o cliente recebe.
 *
 * O nome da oficina vem do cadastro (D-49) em vez de um "aqui é da oficina"
 * genérico: é o primeiro contato, e é ele que dá legitimidade a um link que
 * chega pelo WhatsApp com um token estranho na URL. O mesmo nome aparece no
 * topo da página que o cliente abre em seguida.
 *
 * O prazo sai do `expiresAt` do próprio orçamento. Estava escrito à mão como
 * "7 dias", enquanto o valor real vinha da constante do QuotesController —
 * batiam por coincidência, e mudar a constante faria a mensagem mentir.
 */
export function quoteMessage(quote: {
  workshopName: string
  customerName: string
  serviceOrderNumber: number
  vehicleDescription: string
  vehiclePlate: string
  totalAmount: number
  publicToken: string
  expiresAt: string
}): string {
  const firstName = quote.customerName.trim().split(/\s+/)[0]
  const until = new Date(quote.expiresAt).toLocaleDateString('pt-BR')

  return [
    `Olá, ${firstName}! Aqui é da ${quote.workshopName}.`,
    `O orçamento da OS-${quote.serviceOrderNumber} (${quote.vehicleDescription} · ${quote.vehiclePlate}) ficou em ${money(quote.totalAmount)}.`,
    `Você pode aprovar ou recusar por este link: ${publicQuoteUrl(quote.publicToken)}`,
    `O link vale até ${until}.`,
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
