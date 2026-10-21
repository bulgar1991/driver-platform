import { IconName } from './icon.model'

/** A published review: first name and last initial only. */
export interface Testimonial {
  author: string
  rating: number
  comment: string
  date: string
  vehicleCategory: string
}

export type DriverLicenceKind = 'Ncc' | 'Taxi' | 'ProfessionalOnly'

export interface DriverApplicationRequest {
  firstName: string
  lastName: string
  phone: string
  email: string
  city: string
  licence: DriverLicenceKind
  hasOwnVehicle: boolean
  vehicle: string | null
  yearsExperience: number
  languages: string[]
  message: string | null
  privacyConsent: boolean
  /** Honeypot: always empty for people. */
  website: string
}

export type DriverApplicationStatus =
  'New' | 'Contacted' | 'Accepted' | 'Rejected'

export interface DriverApplication extends Omit<
  DriverApplicationRequest,
  'privacyConsent' | 'website'
> {
  id: string
  status: DriverApplicationStatus
  createdAt: string
}

export interface Stat {
  value: number
  suffix?: string
  label: string
}

export interface Feature {
  icon: IconName
  title: string
  text: string
}

/** A partner logo shown on the home page (only with the partner's permission). */
export interface PartnerLogo {
  name: string
  /** Path under /assets, e.g. "/assets/partners/hotel.svg". */
  logo: string
  url?: string
}
