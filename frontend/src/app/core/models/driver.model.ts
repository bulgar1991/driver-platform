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

export type DriverContractType =
  'Employee' | 'FixedTerm' | 'Freelance' | 'Agency'

/** Where shift messages go; email only when the driver has an address. */
export type DriverNotify = 'WhatsAppAndEmail' | 'WhatsApp' | 'Email'

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
  employeeNumber?: string | null
  /** Tax code or social security number. */
  taxCode?: string | null
  /** yyyy-MM-dd */
  dateOfBirth?: string | null
  placeOfBirth?: string | null
  hiredOn?: string | null
  /** Last working day. */
  leftOn?: string | null
  contractType: DriverContractType
  maxDrivingHours?: number | null
  hourlyCost?: number | null
  jobFunction?: string | null
  /** Left out of the attendance calendar. */
  excludeFromAttendance: boolean
  address?: string | null
  homePhone?: string | null
  notifyBy: DriverNotify
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
  /** Local time at the pickup, yyyy-MM-ddTHH:mm:ss */
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
  /** What the driver earns for the ride; null if not typed yet (always null for dispatchers). */
  driverPay: number | null
}

/** A driver's rides in one month; totals count completed rides only. */
export interface DriverRides {
  year: number
  month: number
  rides: DriverRide[]
  completedRides: number
  completedKm: number
  /** The month's salary (completed rides). Null for dispatchers. */
  payTotal: number | null
  /** Completed rides with no pay typed yet. Null for dispatchers. */
  ridesWithoutPay: number | null
}

/** One driver's line in the monthly salary list. */
export interface DriverPayLine {
  driverId: string
  driverName: string
  completedRides: number
  completedKm: number
  payTotal: number
  ridesWithoutPay: number
}

export type AssignmentStatus =
  'Draft' | 'Sent' | 'Seen' | 'Confirmed' | 'Completed' | 'Cancelled'
