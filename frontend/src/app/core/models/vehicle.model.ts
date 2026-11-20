/**
 * A tariff class code. Built in: Sedan, Business, Estate, Van (shown as Standard, Business, VIP,
 * Bus); more are added in Admin → Pricing ("Minibus16"…).
 */
export type VehicleCategory = string

/** The platform's own classes, cheapest first. */
export const BUILT_IN_CATEGORIES = [
  'Sedan',
  'Business',
  'Estate',
  'Van',
] as const

/** A tariff's names set in the panel; empty ones show the built-in name. */
export interface VehicleClassNames {
  en: string | null
  it: string | null
  de: string | null
  fr: string | null
}

/** A class for the staff's forms (cars, zones): all of them, hidden ones too. */
export interface VehicleClass {
  category: VehicleCategory
  names: VehicleClassNames
  isBuiltIn: boolean
  isActive: boolean
  maxPassengers: number
  maxLuggage: number
}

export interface VehicleRequest {
  plateNumber: string
  make: string
  model: string
  color?: string | null
  category: VehicleCategory
  seats: number
  luggageCapacity: number
  childSeatsAvailable: number
  /** yyyy-MM-dd */
  insuranceExpiresOn?: string | null
  inspectionExpiresOn?: string | null
  nextServiceOn?: string | null
  mileageKm: number
  isActive: boolean
  notes?: string | null
}

export interface Vehicle extends VehicleRequest {
  id: string
  /** Picture at GET /api/photos/{id}. */
  photoId: string | null
  documentAlerts: number
}
