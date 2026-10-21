import { ChildSeatRequest } from './booking.model'
import { GeoPoint, MeetingPoint } from './geo.model'
import { LatLng } from './map.model'
import { ExtraType } from './pricing.model'
import { VehicleCategory } from './vehicle.model'
import { Zone } from './zone.model'

export interface ReturnLegInput {
  /** Italian local time without offset, e.g. "2026-11-02T14:30:00". */
  pickupAt: string
  nameSign: boolean
  flightNumber?: string | null
}

export interface QuoteRequest {
  pickup: GeoPoint
  dropoff: GeoPoint
  /** Italian local time without offset. */
  pickupAt: string
  passengers: number
  largeLuggage: number
  smallLuggage: number
  childSeats: ChildSeatRequest[]
  nameSign: boolean
  pickupMeetingPoint?: string | null
  dropoffMeetingPoint?: string | null
  return?: ReturnLegInput | null
}

export interface QuoteLeg {
  pickup: GeoPoint
  dropoff: GeoPoint
  pickupAt: string
  pickupAtUtc: string
  pickupZone: Zone | null
  dropoffZone: Zone | null
  pickupMeetingPoint: MeetingPoint | null
  dropoffMeetingPoint: MeetingPoint | null
  distanceKm: number
  durationMinutes: number
  routeIsEstimate: boolean
  path: LatLng[]
  surcharges: { name: string; percent: number }[]
  surchargePercent: number
}

export type PriceLineKind =
  'Fare' | 'Surcharge' | 'Discount' | 'ZoneFee' | 'Extra'

export interface PriceLine {
  kind: PriceLineKind
  label: string
  quantity: number
  amount: number
}

export interface LegPrice {
  fare: number
  surchargePercent: number
  surchargeTotal: number
  discountTotal: number
  zoneFeesTotal: number
  extrasTotal: number
  total: number
  lines: PriceLine[]
}

export interface VehicleOption {
  category: VehicleCategory
  maxPassengers: number
  maxLuggage: number
  available: boolean
  unavailableReason: string | null
  total: number
  vatIncluded: number
  outbound: LegPrice
  return: LegPrice | null
}

export interface Quote {
  outbound: QuoteLeg
  return: QuoteLeg | null
  vehicles: VehicleOption[]
  currency: string
  vatRatePercent: number
}

export interface BookingOptions {
  vehicles: {
    category: VehicleCategory
    maxPassengers: number
    maxLuggage: number
  }[]
  extras: Partial<Record<ExtraType, number>>
  returnDiscountPercent: number
  minLeadTimeHours: number
  currency: string
}
