import { Pipe, PipeTransform } from '@angular/core'
import { SITE_CURRENCY } from '@/core/currency'

const formatters = new Map<string, Intl.NumberFormat>()

/** Number style of the site language: each build sets $localize.locale ("it", "de"…). */
function numberLocale(): string {
  const locale = (typeof $localize !== 'undefined' && $localize.locale) || 'en'
  // English prices in the European style of the business: "€58.00", not "€58.00 EUR" or "58.00 €".
  return locale.startsWith('en') ? 'en-IE' : locale
}

/** 58 → "€58.00" (English), "58,00 €" (Italian, German, French); "CHF 58.00" / "58,00 CHF" in francs. */
@Pipe({ name: 'money' })
export class MoneyPipe implements PipeTransform {
  transform(
    value: number | null | undefined,
    currency: string = SITE_CURRENCY
  ): string {
    if (value === null || value === undefined) {
      return ''
    }
    const locale = numberLocale()
    const key = `${locale}:${currency}`
    let formatter = formatters.get(key)
    if (!formatter) {
      formatter = new Intl.NumberFormat(locale, { style: 'currency', currency })
      formatters.set(key, formatter)
    }
    return formatter.format(value)
  }
}
