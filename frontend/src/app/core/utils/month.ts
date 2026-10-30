import { todayInItaly } from './italy-time'

/** The current month in Italy, as yyyy-MM (what <input type="month"> uses). */
export function thisMonthInItaly(): string {
  return todayInItaly().slice(0, 7)
}

/** "2026-10" → [2026, 10]. */
export function splitMonth(month: string): [number, number] {
  const [year, m] = month.split('-').map(Number)
  return [year, m]
}

/** "2026-10", -1 → "2026-09". */
export function addMonths(month: string, delta: number): string {
  const [year, m] = splitMonth(month)
  return new Date(Date.UTC(year, m - 1 + delta, 1)).toISOString().slice(0, 7)
}

export function isMonth(value: string | null | undefined): value is string {
  return !!value && /^\d{4}-(0[1-9]|1[0-2])$/.test(value)
}
