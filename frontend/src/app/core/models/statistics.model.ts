export type StatisticsBucket = 'Day' | 'Week' | 'Month'

/** The headline numbers of a period; cancelled rides count only in `cancelled`. */
export interface StatisticsTotals {
  rides: number
  revenue: number
  averageRide: number
  km: number
  passengers: number
  cancelled: number
  /** 0–1. */
  cancelledRate: number
  customers: number
  newCustomers: number
  refunded: number
  /** Admins only. */
  driverPay: number | null
}

export interface StatisticsPoint {
  /** First office day of the bucket (yyyy-MM-dd). */
  start: string
  rides: number
  revenue: number
  previousRides: number
  previousRevenue: number
}

export interface StatisticsShare {
  /** Enum name, or partner / driver / vehicle id. */
  key: string
  label: string
  rides: number
  revenue: number
  km: number
  pay: number | null
}

export interface StatisticsResult {
  from: string
  to: string
  previousFrom: string
  previousTo: string
  totals: StatisticsTotals
  previous: StatisticsTotals
  bucket: StatisticsBucket
  series: StatisticsPoint[]
  channels: StatisticsShare[]
  categories: StatisticsShare[]
  drivers: StatisticsShare[]
  vehicles: StatisticsShare[]
  /** Rides by weekday (0 = Monday) and hour. */
  heatmap: number[][]
  /** Booked <1 day, 1–3, 4–7, 8–30, >30 days before the pickup. */
  leadTime: number[]
  unassigned: number
  currency: string
}
