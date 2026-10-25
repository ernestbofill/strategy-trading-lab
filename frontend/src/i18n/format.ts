/**
 * Fills a message: `{name}` takes `params.name`, and `{n, plural, one {# day} other {# days}}` picks the form for the
 * number `n` (`#` is the number). English and Catalan both use the forms `one` and `other`.
 */
export type MessageParams = Record<string, string | number>

/** The text between the brace at `start` and its match, and where the match is. */
function block(text: string, start: number): { inner: string; end: number } {
  let depth = 0
  for (let i = start; i < text.length; i++) {
    if (text[i] === '{') depth++
    else if (text[i] === '}' && --depth === 0) return { inner: text.slice(start + 1, i), end: i }
  }
  return { inner: text.slice(start + 1), end: text.length - 1 }
}

function plural(inner: string, params: MessageParams, locale: string): string {
  const [name, , ...rest] = inner.split(',')
  const n = Number(params[name.trim()])
  const forms = rest.join(',')
  const chosen: Record<string, string> = {}
  for (let i = 0; i < forms.length; i++) {
    const open = forms.indexOf('{', i)
    if (open < 0) break
    const selector = forms.slice(i, open).trim()
    const { inner: text, end } = block(forms, open)
    chosen[selector] = text
    i = end
  }
  const form = chosen[`=${n}`] ?? chosen[new Intl.PluralRules(locale).select(n)] ?? chosen.other ?? ''
  const replaced = form.replaceAll('#', String(n))
  return formatMessage(replaced, params, locale)
}

export function formatMessage(message: string, params: MessageParams | undefined, locale: string): string {
  if (!params || !message.includes('{')) return message
  let out = ''
  for (let i = 0; i < message.length; i++) {
    if (message[i] !== '{') {
      out += message[i]
      continue
    }
    const { inner, end } = block(message, i)
    out += inner.includes(',') ? plural(inner, params, locale) : String(params[inner.trim()] ?? `{${inner}}`)
    i = end
  }
  return out
}
