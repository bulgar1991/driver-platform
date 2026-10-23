/** "2026-11-02" + "14:30" → "2026-11-02T14:30:00" (Italian local time, no offset). */
export function toLocalDateTime(date: string, time: string): string {
  return `${date}T${time.length === 5 ? `${time}:00` : time}`
}

/** Today's date in Italy as yyyy-MM-dd, whatever the browser's time zone. */
export function todayInItaly(offsetDays = 0): string {
  const date = new Date(Date.now() + offsetDays * 86_400_000)
  return new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Europe/Rome',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(date)
}

/** The time in Italy a number of minutes from now, as "yyyy-MM-ddTHH:mm", whatever the browser's time zone. */
export function nowInItaly(offsetMinutes = 0): string {
  const parts = new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Europe/Rome',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    hourCycle: 'h23',
  }).formatToParts(new Date(Date.now() + offsetMinutes * 60_000))
  const part = (type: Intl.DateTimeFormatPartTypes) =>
    parts.find(p => p.type === type)?.value ?? ''
  return `${part('year')}-${part('month')}-${part('day')}T${part('hour')}:${part('minute')}`
}
