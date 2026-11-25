import { BookingSource, BookingStatus, ChildSeatRequest } from './booking.model'
import { AssignmentStatus } from './driver.model'
import { MessageSummary } from './driver-portal.model'
import { GeoPoint, MeetingPoint } from './geo.model'
import { PaymentMethod, PaymentStatus } from './payment.model'
import { PriceLine, QuoteRequest } from './quote.model'
import { VehicleCategory } from './vehicle.model'

export interface PagedResult<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}

export interface BookingQuery {
  /** yyyy-MM-dd, Italian dates, inclusive. */
  from?: string | null
  to?: string | null
  status?: BookingStatus | null
  unassigned?: boolean
  includeUnpaid?: boolean
  search?: string | null
  page?: number
  pageSize?: number
}

export interface BookingListItem {
  id: string
  reference: string
  status: BookingStatus
  source: BookingSource
  /** Local time at the pickup. */
  pickupAt: string
  pickupLabel: string
  dropoffLabel: string
  clientName: string
  clientPhone: string
  passengers: number
  largeLuggage: number
  smallLuggage: number
  childSeats: number
  vehicleCategory: VehicleCategory
  distanceKm: number
  durationMinutes: number
  totalPrice: number
  flightNumber: string | null
  assignmentId: string | null
  driverName: string | null
  vehiclePlate: string | null
  isReturnLeg: boolean
  /** The partner or Booking that sent it; null for the website. */
  partnerName: string | null
}

export interface BookingDetail {
  id: string
  reference: string
  status: BookingStatus
  source: BookingSource
  pickupAt: string
  pickup: GeoPoint
  dropoff: GeoPoint
  pickupMeetingPoint: MeetingPoint | null
  dropoffMeetingPoint: MeetingPoint | null
  passengers: number
  largeLuggage: number
  smallLuggage: number
  childSeats: ChildSeatRequest[]
  flightNumber: string | null
  /** As on the ticket: the airport's local time. */
  flightDepartsAt: string | null
  flightLandsAt: string | null
  nameSign: boolean
  nameSignText: string | null
  clientNotes: string | null
  internalNotes: string | null
  vehicleCategory: VehicleCategory
  distanceKm: number
  durationMinutes: number
  totalPrice: number
  currency: string
  priceLines: PriceLine[]
  client: {
    name: string
    email: string | null
    phone: string
    language: string
    /** Kept on the client: the driver sees them on every ride of theirs. */
    notesForDriver: string | null
    /** All of the client's bookings, this one included. */
    rides: number
  }
  paymentStatus: PaymentStatus | null
  assignmentId: string | null
  driverName: string | null
  vehiclePlate: string | null
  linkedReference: string | null
  cancellationReason: string | null
  createdAt: string
  partnerName: string | null
  externalReference: string | null
  paymentId: string | null
  paymentMethod: PaymentMethod | null
  /** The driver collects the price at the ride. */
  collectOnBoard: boolean
}

export interface ShiftRequest {
  driverId: string
  vehicleId: string
  bookingIds: string[]
  notes?: string | null
}

export interface Shift {
  id: string
  status: AssignmentStatus
  shiftDate: string
  startsAt: string
  endsAt: string
  driverId: string
  driverName: string
  driverPhone: string
  vehicleId: string
  vehicleLabel: string
  vehiclePlate: string
  vehicleCategory: VehicleCategory
  notes: string | null
  bookings: BookingListItem[]
  sentAt: string | null
  seenAt: string | null
  confirmedAt: string | null
  completedAt: string | null
  /** Latest message to the driver per channel. */
  messages: MessageSummary[]
}

export interface ShiftResult {
  shift: Shift
  warnings: string[]
}

export interface DriverOption {
  id: string
  name: string
  available: boolean
  problems: string[]
}

export interface VehicleOptionAvailability {
  id: string
  label: string
  plateNumber: string
  category: VehicleCategory
  available: boolean
  problems: string[]
}

export interface Availability {
  drivers: DriverOption[]
  vehicles: VehicleOptionAvailability[]
}

export interface DocumentAlert {
  owner: string
  kind: 'Driver' | 'Vehicle'
  ownerId: string
  document: string
  expiresOn: string
  expired: boolean
}

export interface Dashboard {
  todayRides: number
  todayCompleted: number
  inProgress: number
  unassignedNext48h: number
  awaitingPayment: number
  shiftsToConfirm: number
  nextUnassigned: BookingListItem[]
  documentAlerts: DocumentAlert[]
}

/** A new customer registered with an office booking (the "Customer record" popup). */
export interface OfficeCustomerInput {
  firstName: string
  lastName: string
  phone: string
  email: string | null
  language: string
  company: string | null
  taxCode: string | null
  vatNumber: string | null
  recipientCode: string | null
  pecEmail: string | null
  billingEmail: string | null
  address: string | null
  postalCode: string | null
  city: string | null
  province: string | null
  country: string | null
  notes: string | null
}

/** Admin → Bookings → New booking: a ride taken by phone, WhatsApp or email. */
export interface OfficeBookingRequest {
  /** An existing customer; otherwise `customer` is found by phone or created. */
  clientId: string | null
  customer: OfficeCustomerInput | null
  trip: QuoteRequest
  vehicleCategory: VehicleCategory
  partnerId: string | null
  externalReference: string | null
  passengerName: string | null
  flightNumber: string | null
  /** VAT included; null keeps the tariff price. */
  price: number | null
  additionalCosts: { description: string; amount: number }[]
  paymentMethod: PaymentMethod
  driverNotes: string | null
  internalNotes: string | null
}

export interface OfficeBookingResult {
  id: string
  reference: string
  status: BookingStatus
  total: number
  /** Stripe's payment page to send the client, when paid online. */
  paymentUrl: string | null
}
