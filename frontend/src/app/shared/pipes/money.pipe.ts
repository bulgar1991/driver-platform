import { Pipe, PipeTransform } from '@angular/core'

const formatters = new Map<string, Intl.NumberFormat>()

/** 58 → "€58.00". */
@Pipe({ name: 'money' })
export class MoneyPipe implements PipeTransform {
  transform(value: number | null | undefined, currency = 'EUR'): string {
    if (value === null || value === undefined) {
      return ''
    }
    let formatter = formatters.get(currency)
    if (!formatter) {
      formatter = new Intl.NumberFormat('en-IE', {
        style: 'currency',
        currency,
      })
      formatters.set(currency, formatter)
    }
    return formatter.format(value)
  }
}
