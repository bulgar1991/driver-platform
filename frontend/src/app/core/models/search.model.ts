import { AssignmentStatus } from './driver.model'
import { BookingSource, BookingStatus } from './booking.model'
import { PaymentStatus } from './payment.model'
import { VehicleCategory } from './vehicle.model'

export type PaymentFilter = 'Any' | 'Paid' | 'Refunded' | 'NotPaidOnline'
export type ReportKind =
  'List' | 'DailySheet' | 'Driver' | 'Partner' | 'Vehicle'

export interface SearchQuery {
  /** yyyy-MM-dd, office dates of the pickup; at most 366 days. */
  from: string
  to: string
  text?: string
  statuses?: BookingStatus[]
  source?: BookingSource | null
  partnerId?: string | null
  driverId?: string | null
  vehicleId?: string | null
  category?: VehicleCategory | null
  payment?: PaymentFilter
  unassigned?: boolean
  includeCancelled?: boolean
}

export interface SearchRide {
  id: string
  reference: string
  status: BookingStatus
  source: BookingSource
  /** Local time at the pickup. */
  pickupLocal: string
  durationMinutes: number
  pickupLabel: string
  dropoffLabel: string
  clientName: string
  clientPhone: string
  clientEmail: string | null
  partnerName: string | null
  externalReference: string | null
  passengers: number
  largeLuggage: number
  smallLuggage: number
  vehicleCategory: VehicleCategory
  distanceKm: number
  totalPrice: number
  /** Admins only. */
  driverPay: number | null
  paymentStatus: PaymentStatus | null
  flightNumber: string | null
  driverId: string | null
  driverName: string | null
  vehicleId: string | null
  vehiclePlate: string | null
  shiftStatus: AssignmentStatus | null
}

export interface SearchTotals {
  rides: number
  cancelled: number
  revenue: number
  driverPay: number | null
  km: number
  passengers: number
}

export interface SearchResult {
  rides: SearchRide[]
  /** More matched than returned (2,000). */
  truncated: boolean
  totals: SearchTotals
  currency: string
  /** PDF reports these filters allow. */
  reports: ReportKind[]
}

export interface SearchOption {
  id: string
  name: string
  isActive: boolean
}

export interface SearchOptions {
  partners: SearchOption[]
  drivers: SearchOption[]
  vehicles: SearchOption[]
}
