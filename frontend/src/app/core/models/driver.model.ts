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
  /** Documents expired or expiring within 30 days. */
  documentAlerts: number
  /** Average client rating 1–5, null before the first review. */
  rating: number | null
  ratingCount: number
  completedRides: number
  cancelledShifts: number
}

export type AssignmentStatus =
  'Draft' | 'Sent' | 'Seen' | 'Confirmed' | 'Completed' | 'Cancelled'
