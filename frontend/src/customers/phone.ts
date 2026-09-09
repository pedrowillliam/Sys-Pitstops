/** O banco guarda só dígitos (PhoneNumber.Normalize no backend); a pontuação é
 *  coisa de tela. O estado do formulário segue a mesma regra, e a máscara é
 *  aplicada na hora de desenhar. */
export function onlyDigits(value: string): string {
  return value.replace(/\D/g, '')
}

/** DDD (2) + número: 9 dígitos no celular, 8 no fixo. */
export const phoneMaxLength = 11

/** Dígitos que o campo aceita guardar.
 *
 *  Espelha PhoneNumber.StripCountryCode, e precisa existir aqui porque o corte
 *  em 11 dígitos acontece antes de o backend ver o número: sem isto, colar
 *  "+55 81 99999-0000" viraria "(55) 81999-9900" caladamente.
 *
 *  O DDI só é descartado quando o campo ganhou mais de um dígito de uma vez,
 *  que é o que caracteriza uma colagem. Digitando não há como confundir: 55 é
 *  DDD de verdade (Santa Maria), e ninguém digita o país por engano. */
export function toPhoneDigits(value: string, previous = ''): string {
  const digits = onlyDigits(value)
  const pasted = digits.length > onlyDigits(previous).length + 1

  const local =
    pasted && (digits.length === 12 || digits.length === 13) && digits.startsWith('55')
      ? digits.slice(2)
      : digits

  return local.slice(0, phoneMaxLength)
}

/** Escreve "(81) 99999-0000" enquanto o atendente digita. Os parênteses são o
 *  que deixa claro, no próprio campo, que os dois primeiros dígitos são o DDD —
 *  sem ele o cadastro é recusado pelo backend. */
export function formatPhone(value: string): string {
  const digits = onlyDigits(value).slice(0, phoneMaxLength)

  if (digits.length <= 2) {
    return digits === '' ? '' : `(${digits}`
  }

  const ddd = digits.slice(0, 2)
  const rest = digits.slice(2)
  // Até o oitavo dígito o número é tratado como fixo (4+4). O nono empurra o
  // corte para 5+4, que é o formato do celular.
  const head = rest.length > 8 ? 5 : 4

  return rest.length <= head
    ? `(${ddd}) ${rest}`
    : `(${ddd}) ${rest.slice(0, head)}-${rest.slice(head)}`
}
