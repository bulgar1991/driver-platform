export interface CountItem {
  key: string
  count: number
}

export interface CityItem {
  country: string | null
  city: string
  count: number
}

export interface DayItem {
  date: string
  visits: number
  visitors: number
}

export interface RecentVisit {
  at: string
  path: string
  referrer: string | null
  /** Cleared after 30 days. */
  ip: string | null
  country: string | null
  city: string | null
  device: string
  browser: string | null
  os: string | null
  language: string | null
}

/** Admin → Visitors for the last `days` days. */
export interface VisitorSummary {
  days: number
  visits: number
  visitors: number
  visitsToday: number
  byDay: DayItem[]
  countries: CountItem[]
  cities: CityItem[]
  pages: CountItem[]
  referrers: CountItem[]
  devices: CountItem[]
  browsers: CountItem[]
  recent: RecentVisit[]
}
