import { CalendarAbsence, CalendarData, CalendarRide } from '@/core/models'

export type Period = 'day' | 'days2' | 'days3' | 'week' | 'weeks2' | 'month'
export type GroupBy = 'vehicles' | 'drivers' | 'customers' | 'category'

const DAY = 86_400_000

/** Wall-clock minutes since 1970 of an office-local "yyyy-MM-ddTHH:mm[:ss]" (time zone ignored on purpose). */
export function wallMinutes(local: string): number {
  return (
    Date.parse(
      local.length === 10 ? local + 'T00:00:00Z' : local.slice(0, 19) + 'Z'
    ) / 60_000
  )
}

export function addDays(date: string, days: number): string {
  return new Date(Date.parse(date + 'T00:00:00Z') + days * DAY)
    .toISOString()
    .slice(0, 10)
}

/** Monday of the week of `date`. */
export function mondayOf(date: string): string {
  const day = new Date(date + 'T00:00:00Z').getUTCDay()
  return addDays(date, -((day + 6) % 7))
}

/** First and last day shown for a period around `anchor`. */
export function periodRange(
  anchor: string,
  period: Period
): { from: string; to: string } {
  switch (period) {
    case 'day':
      return { from: anchor, to: anchor }
    case 'days2':
      return { from: anchor, to: addDays(anchor, 1) }
    case 'days3':
      return { from: anchor, to: addDays(anchor, 2) }
    case 'week': {
      const from = mondayOf(anchor)
      return { from, to: addDays(from, 6) }
    }
    case 'weeks2': {
      const from = mondayOf(anchor)
      return { from, to: addDays(from, 13) }
    }
    case 'month': {
      const [y, m] = anchor.split('-').map(Number)
      const last = new Date(Date.UTC(y, m, 0)).getUTCDate()
      const mm = String(m).padStart(2, '0')
      return {
        from: `${y}-${mm}-01`,
        to: `${y}-${mm}-${String(last).padStart(2, '0')}`,
      }
    }
  }
}

/** The anchor one period earlier (-1) or later (+1). */
export function shiftAnchor(
  anchor: string,
  period: Period,
  step: number
): string {
  if (period === 'month') {
    const [y, m] = anchor.split('-').map(Number)
    const d = new Date(Date.UTC(y, m - 1 + step, 1))
    return d.toISOString().slice(0, 10)
  }
  const days = { day: 1, days2: 2, days3: 3, week: 7, weeks2: 14 }[period]
  return addDays(anchor, step * days)
}

/** A tick on the time axis: where it sits (0–100 %) and what it says. */
export interface Tick {
  left: number
  label: string
  /** Starts a new day: drawn stronger. */
  major: boolean
  date: string
  weekend: boolean
}

export function ticks(from: string, to: string): Tick[] {
  const days = (Date.parse(to) - Date.parse(from)) / DAY + 1
  const total = days * 24
  const step = days === 1 ? 1 : days <= 3 ? 3 : 24
  const result: Tick[] = []
  for (let h = 0; h < total; h += step) {
    const date = addDays(from, Math.floor(h / 24))
    const dow = new Date(date + 'T00:00:00Z').getUTCDay()
    result.push({
      left: (h / total) * 100,
      label:
        h % 24 === 0 && step !== 1 ? date : String(h % 24).padStart(2, '0'),
      major: h % 24 === 0,
      date,
      weekend: dow === 0 || dow === 6,
    })
  }
  return result
}

/** A ride placed on the timeline. */
export interface Bar {
  ride: CalendarRide
  left: number
  width: number
  lane: number
  /** Starts before or ends after the period. */
  clippedStart: boolean
  clippedEnd: boolean
}

export interface BackdropBlock {
  left: number
  width: number
  color: string
  label: string
}

export interface Row {
  key: string
  title: string
  subtitle: string
  /** The "not assigned yet" lane: shown first and highlighted. */
  pending: boolean
  bars: Bar[]
  lanes: number
  /** Absences of a driver, drawn behind the rides. */
  backdrop: BackdropBlock[]
}

export interface RowLabels {
  notAssigned: string
  categoryName: (code: string) => string
}

/** Builds the rows of the timeline: one per resource, rides stacked in lanes when they overlap. */
export function buildRows(
  data: CalendarData,
  rides: CalendarRide[],
  groupBy: GroupBy,
  labels: RowLabels,
  /** Shortest bar on screen, in minutes of the axis: room for the time and a name. */
  minMinutes = 15
): Row[] {
  const start = wallMinutes(data.from)
  const span = wallMinutes(data.to) + 24 * 60 - start
  const groups = new Map<
    string,
    {
      title: string
      subtitle: string
      pending: boolean
      order: number
      rides: CalendarRide[]
    }
  >()
  const add = (
    key: string,
    title: string,
    subtitle: string,
    order: number,
    pending = false
  ) => {
    if (!groups.has(key)) {
      groups.set(key, { title, subtitle, pending, order, rides: [] })
    }
    return groups.get(key)!
  }
  const pendingKey = '~pending'
  if (groupBy === 'vehicles' || groupBy === 'drivers') {
    add(pendingKey, labels.notAssigned, '', -1, true)
  }

  if (groupBy === 'vehicles') {
    const used = new Set(rides.map(r => r.vehicleId))
    data.vehicles
      .filter(v => v.isActive || used.has(v.id))
      .forEach((v, i) =>
        add(
          v.id,
          v.code ? `${v.code} · ${v.plateNumber}` : v.plateNumber,
          `${v.name} · ${labels.categoryName(v.category)}`,
          i
        )
      )
  } else if (groupBy === 'drivers') {
    const used = new Set(rides.map(r => r.driverId))
    data.drivers
      .filter(d => d.isActive || used.has(d.id))
      .forEach((d, i) => add(d.id, d.name, '', i))
  }

  for (const ride of rides) {
    let group
    switch (groupBy) {
      case 'vehicles':
        group = ride.vehicleId
          ? groups.get(ride.vehicleId)
          : groups.get(pendingKey)
        break
      case 'drivers':
        group = ride.driverId
          ? groups.get(ride.driverId)
          : groups.get(pendingKey)
        break
      case 'customers': {
        const key = ride.partnerId ?? ride.clientId
        group = add(
          key,
          ride.partnerName ?? ride.clientName,
          ride.partnerName ? ride.clientName : '',
          0
        )
        break
      }
      case 'category':
        group = add(
          ride.vehicleCategory,
          labels.categoryName(ride.vehicleCategory),
          '',
          0
        )
        break
    }
    group?.rides.push(ride)
  }

  const absences =
    groupBy === 'drivers'
      ? absenceBlocks(data.absences, start, span)
      : new Map<string, BackdropBlock[]>()
  const rows = [...groups.entries()].map(([key, g]) => {
    const { bars, lanes } = place(g.rides, start, span, minMinutes)
    return {
      key,
      title: g.title,
      subtitle: g.subtitle,
      pending: g.pending,
      bars,
      lanes,
      backdrop: absences.get(key) ?? [],
      order: g.order,
    }
  })
  if (groupBy === 'customers' || groupBy === 'category') {
    rows.sort((a, b) => a.title.localeCompare(b.title))
  } else {
    rows.sort((a, b) => a.order - b.order)
  }
  return rows
}

/** Greedy lanes: each ride goes in the first lane free at its start. */
export function place(
  rides: CalendarRide[],
  start: number,
  span: number,
  minMinutes = 15
): { bars: Bar[]; lanes: number } {
  const laneEnds: number[] = []
  const bars: Bar[] = []
  for (const ride of [...rides].sort((a, b) =>
    a.start.localeCompare(b.start)
  )) {
    const from = wallMinutes(ride.start)
    // Lanes use the drawn length, so a short ride's label never sits under the next bar.
    const to = from + Math.max(ride.durationMinutes, minMinutes)
    let lane = laneEnds.findIndex(end => end <= from)
    if (lane === -1) {
      lane = laneEnds.length
      laneEnds.push(to)
    } else {
      laneEnds[lane] = to
    }
    const left = Math.max(0, from - start)
    const right = Math.min(span, to - start)
    bars.push({
      ride,
      left: (left / span) * 100,
      width: Math.max(((right - left) / span) * 100, 0.25),
      lane,
      clippedStart: from < start,
      clippedEnd: to - start > span,
    })
  }
  return { bars, lanes: Math.max(laneEnds.length, 1) }
}

function absenceBlocks(
  absences: CalendarAbsence[],
  start: number,
  span: number
): Map<string, BackdropBlock[]> {
  const map = new Map<string, BackdropBlock[]>()
  for (const a of absences) {
    const dayStart = wallMinutes(a.date)
    const from = a.startTime ? dayStart + minutesOf(a.startTime) : dayStart
    const to = a.endTime ? dayStart + minutesOf(a.endTime) : dayStart + 24 * 60
    const list = map.get(a.driverId) ?? []
    list.push({
      left: ((from - start) / span) * 100,
      width: ((to - from) / span) * 100,
      color: a.color,
      label: a.name,
    })
    map.set(a.driverId, list)
  }
  return map
}

function minutesOf(time: string): number {
  const [h, m] = time.split(':').map(Number)
  return h * 60 + m
}
