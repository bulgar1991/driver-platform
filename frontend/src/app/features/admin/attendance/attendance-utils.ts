import { AttendanceEntry, AttendancePerson } from '@/core/models'

/** Every date from `from` to `to` (yyyy-MM-dd), both included. */
export function daysBetween(from: string, to: string): string[] {
  const days: string[] = []
  const end = Date.parse(to + 'T00:00:00Z')
  for (let t = Date.parse(from + 'T00:00:00Z'); t <= end; t += 86_400_000) {
    days.push(new Date(t).toISOString().slice(0, 10))
  }
  return days
}

/** yyyy-MM-dd plus a number of days. */
export function addDays(date: string, days: number): string {
  return new Date(Date.parse(date + 'T00:00:00Z') + days * 86_400_000)
    .toISOString()
    .slice(0, 10)
}

/** First and last day of the month of `date`. */
export function monthOf(date: string): { from: string; to: string } {
  const [y, m] = date.split('-').map(Number)
  const last = new Date(Date.UTC(y, m, 0)).getUTCDate()
  const mm = String(m).padStart(2, '0')
  return {
    from: `${y}-${mm}-01`,
    to: `${y}-${mm}-${String(last).padStart(2, '0')}`,
  }
}

/** 0 = Sunday … 6 = Saturday. */
export function weekday(date: string): number {
  return new Date(date + 'T00:00:00Z').getUTCDay()
}

export function isWeekend(date: string): boolean {
  const d = weekday(date)
  return d === 0 || d === 6
}

/** Minutes of an event with times; null for a whole day. */
export function entryMinutes(entry: AttendanceEntry): number | null {
  if (!entry.startTime || !entry.endTime) {
    return null
  }
  const toMinutes = (t: string) => {
    const [h, m] = t.split(':').map(Number)
    return h * 60 + m
  }
  return toMinutes(entry.endTime) - toMinutes(entry.startTime)
}

/** 125 → "2h 05'". */
export function formatMinutes(minutes: number): string {
  return `${Math.floor(minutes / 60)}h ${String(minutes % 60).padStart(2, '0')}'`
}

/** "08:30:00" → "08:30". */
export function shortTime(time: string | null): string {
  return time ? time.slice(0, 5) : ''
}

/** Employed on that day (no dates means always). */
export function employedOn(person: AttendancePerson, date: string): boolean {
  return (
    (!person.hiredOn || person.hiredOn <= date) &&
    (!person.leftOn || person.leftOn >= date)
  )
}
