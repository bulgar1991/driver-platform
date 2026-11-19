/**
 * Times without an offset, as people read them on a clock. Ride times are in the local time where
 * the ride starts (the API gives the zone: GeoService.timeZone); "today" in the panel, reports and
 * shift days are in the company's own zone (TIME_ZONE, set per server at build time).
 */
export const OFFICE_TIME_ZONE =
  typeof TIME_ZONE === 'string' && TIME_ZONE.trim()
    ? TIME_ZONE.trim()
    : 'Europe/Rome'

/** "2026-11-02" + "14:30" → "2026-11-02T14:30:00" (local time, no offset). */
export function toLocalDateTime(date: string, time: string): string {
  return `${date}T${time.length === 5 ? `${time}:00` : time}`
}

/** "2026-11-02T14:30" from a datetime-local field (a time as on a ticket, no offset) → "2026-11-02T14:30:00"; null when empty. */
export function ticketTime(value: string): string | null {
  const [date, time] = value.split('T')
  return date && time ? toLocalDateTime(date, time.slice(0, 5)) : null
}

/** Today's date in a time zone as yyyy-MM-dd, whatever the browser's own zone. */
export function todayIn(zone: string, offsetDays = 0): string {
  const date = new Date(Date.now() + offsetDays * 86_400_000)
  return new Intl.DateTimeFormat('en-CA', {
    timeZone: zone,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(date)
}

/** Today's date for the company (OFFICE_TIME_ZONE) as yyyy-MM-dd. */
export function todayInOffice(offsetDays = 0): string {
  return todayIn(OFFICE_TIME_ZONE, offsetDays)
}

/** The time in a time zone a number of minutes from now, as "yyyy-MM-ddTHH:mm". */
export function nowIn(zone: string, offsetMinutes = 0): string {
  const parts = new Intl.DateTimeFormat('en-CA', {
    timeZone: zone,
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
