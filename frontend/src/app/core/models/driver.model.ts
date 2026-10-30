import { BookingStatus } from './booking.model'

export type DriverDocumentType =
  | 'DrivingLicence'
  | 'NccLicence'
  | 'TaxiLicence'
  | 'ProfessionalCertificate'
  | 'Insurance'
  | 'CriminalRecord'

export interface DriverDocument {
  id?: string | null
  type: DriverDocumentType
  number?: string | null
  /** yyyy-MM-dd */
  expiresOn?: string | null
}

export interface DriverRequest {
  firstName: string
  lastName: string
  phone: string
  email?: string | null
  languages: string[]
  preferredAreas: string[]
  commissionPercent: number
  isActive: boolean
  isAvailable: boolean
  notes?: string | null
  documents: DriverDocument[]
}

export interface Driver extends DriverRequest {
  id: string
  /** Picture at GET /api/photos/{id}. */
  photoId: string | null
  /** Documents expired or expiring within 30 days. */
  documentAlerts: number
  /** Average client rating 1–5, null before the first review. */
  rating: number | null
  ratingCount: number
  completedRides: number
  cancelledShifts: number
}

/** One booking on a driver's shift. */
export interface DriverRide {
  bookingId: string
  reference: string
  /** Italian local time, yyyy-MM-ddTHH:mm:ss */
  pickupAt: string
  pickupAddress: string
  dropoffAddress: string
  distanceKm: number
  durationMinutes: number
  status: BookingStatus
  vehiclePlate: string
  vehicleName: string
  totalPrice: number
  currency: string
}

/** A driver's rides in one month; totals count completed rides only. */
export interface DriverRides {
  year: number
  month: number
  rides: DriverRide[]
  completedRides: number
  completedKm: number
}

export type AssignmentStatus =
  'Draft' | 'Sent' | 'Seen' | 'Confirmed' | 'Completed' | 'Cancelled'
