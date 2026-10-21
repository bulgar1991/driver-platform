import { GeoPoint, MeetingPoint } from './geo.model'
import { LegPrice, QuoteRequest } from './quote.model'
import { VehicleCategory } from './vehicle.model'

export type BookingSource = 'Website' | 'Booking' | 'Partner'

export type BookingStatus =
  | 'AwaitingPayment'
  | 'New'
  | 'Assigned'
  | 'PickedUp'
  | 'Completed'
  | 'Cancelled'

export type ChildSeatGroup = 'Infant' | 'Toddler' | 'Booster'

export interface ChildSeatRequest {
  group: ChildSeatGroup
  count: number
}

export interface Booking {
  id: string
  reference: string
  source: BookingSource
  status: BookingStatus
  pickup: GeoPoint
  dropoff: GeoPoint
  pickupMeetingPoint?: MeetingPoint
  dropoffMeetingPoint?: MeetingPoint
  pickupAt: string
  passengers: number
  largeLuggage: number
  smallLuggage: number
  childSeats: ChildSeatRequest[]
  flightNumber?: string
  nameSign: boolean
  vehicleCategory: VehicleCategory
  distanceKm: number
  durationMinutes: number
  totalPrice: number
  currency: string
}

export interface ContactInput {
  firstName: string
  lastName: string
  email: string
  phone: string
  language: string
}

export interface CreateBookingRequest {
  trip: QuoteRequest
  vehicleCategory: VehicleCategory
  /** The total the client saw; the API answers 409 if the price changed. */
  expectedTotal: number | null
  contact: ContactInput
  flightNumber?: string | null
  nameSignText?: string | null
  notes?: string | null
  gdprConsent: boolean
  marketingConsent: boolean
}

export interface BookingLegSummary {
  reference: string
  pickupAt: string
  pickupAddress: string
  dropoffAddress: string
  pickupMeetingPoint: MeetingPoint | null
  dropoffMeetingPoint: MeetingPoint | null
  distanceKm: number
  durationMinutes: number
  price: LegPrice
}

export interface CreateBookingResponse {
  reference: string
  status: BookingStatus
  vehicleCategory: VehicleCategory
  legs: BookingLegSummary[]
  total: number
  vatIncluded: number
  vatRatePercent: number
  currency: string
  /** Stripe's payment page; null if it could not be opened. */
  paymentUrl: string | null
  /** The client's booking page (pay again, invoice). */
  manageUrl: string
}
