import { AssignmentStatus } from './driver.model'
import { BookingSource, BookingStatus } from './booking.model'
import { VehicleCategory } from './vehicle.model'

/** A ride on the services calendar. */
export interface CalendarRide {
  id: string
  reference: string
  status: BookingStatus
  source: BookingSource
  /** Office time zone, yyyy-MM-ddTHH:mm:ss: where the bar sits. */
  start: string
  /** Local time at the pickup, for labels. */
  pickupLocal: string
  durationMinutes: number
  pickupLabel: string
  dropoffLabel: string
  clientId: string
  clientName: string
  partnerId: string | null
  partnerName: string | null
  passengers: number
  largeLuggage: number
  smallLuggage: number
  childSeats: number
  vehicleCategory: VehicleCategory
  distanceKm: number
  totalPrice: number
  /** Admins only. */
  driverPay: number | null
  assignmentId: string | null
  shiftStatus: AssignmentStatus | null
  driverId: string | null
  driverName: string | null
  vehicleId: string | null
  vehiclePlate: string | null
  flightNumber: string | null
}

export interface CalendarVehicle {
  id: string
  plateNumber: string
  code: string | null
  name: string
  category: VehicleCategory
  seats: number
  isActive: boolean
}

export interface CalendarDriver {
  id: string
  name: string
  isActive: boolean
  isAvailable: boolean
}

export interface CalendarAbsence {
  driverId: string
  /** yyyy-MM-dd */
  date: string
  name: string
  code: string
  color: string
  makesUnavailable: boolean
  startTime: string | null
  endTime: string | null
}

export interface CalendarData {
  from: string
  to: string
  currency: string
  rides: CalendarRide[]
  vehicles: CalendarVehicle[]
  drivers: CalendarDriver[]
  absences: CalendarAbsence[]
}
