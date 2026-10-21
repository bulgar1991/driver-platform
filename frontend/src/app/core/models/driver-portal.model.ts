import { BookingStatus, ChildSeatRequest } from './booking.model'
import { AssignmentStatus } from './driver.model'
import { GeoPoint, MeetingPoint } from './geo.model'

export interface DriverBookingView {
  id: string
  reference: string
  status: BookingStatus
  pickupAt: string
  /** The client's exact address; the car stops at the meeting point when there is one. */
  pickup: GeoPoint
  dropoff: GeoPoint
  pickupMeetingPoint: MeetingPoint | null
  dropoffMeetingPoint: MeetingPoint | null
  passengers: number
  largeLuggage: number
  smallLuggage: number
  childSeats: ChildSeatRequest[]
  flightNumber: string | null
  nameSign: boolean
  nameSignText: string | null
  clientName: string
  clientPhone: string
  clientLanguage: string
  clientNotes: string | null
  distanceKm: number
  durationMinutes: number
  canPickUp: boolean
  canComplete: boolean
}

export interface DriverShiftView {
  id: string
  status: AssignmentStatus
  shiftDate: string
  startsAt: string
  endsAt: string
  vehicleLabel: string
  vehiclePlate: string
  notes: string | null
  canConfirm: boolean
  bookings: DriverBookingView[]
}

export interface DriverPortal {
  driverFirstName: string
  shifts: DriverShiftView[]
}

export interface ReviewPage {
  reference: string
  pickupAt: string
  from: string
  to: string
  driverFirstName: string
  submitted: boolean
}

export interface Review {
  id: string
  bookingReference: string
  pickupAt: string
  driverId: string
  driverName: string
  clientName: string
  rating: number | null
  comment: string | null
  channel: NotificationChannel
  sentAt: string
  submittedAt: string | null
}

export type NotificationChannel = 'Email' | 'WhatsApp' | 'Sms'
export type NotificationKind =
  'DriverShift' | 'DriverShiftChanged' | 'ClientDriverDetails' | 'ReviewRequest'
export type NotificationStatus = 'Sent' | 'Failed' | 'Skipped'

export interface MessageSummary {
  kind: NotificationKind
  channel: NotificationChannel
  status: NotificationStatus
  recipient: string
  error: string | null
  createdAt: string
}
