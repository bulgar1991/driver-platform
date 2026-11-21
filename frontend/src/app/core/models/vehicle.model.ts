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
  /** Internal fleet code ("T1"). */
  code?: string | null
  /** yyyy-MM-dd */
  registeredOn?: string | null
  chassisNumber?: string | null
  costPerKm?: number | null
  defaultDriverId?: string | null
  /** Leasing or rental company. */
  leasingCompany?: string | null
  hasDvd: boolean
  hasWifi: boolean
  hasWc: boolean
  automaticGearbox: boolean
  wheelchairRamp: boolean
}

export interface Vehicle extends VehicleRequest {
  id: string
  /** Picture at GET /api/photos/{id}. */
  photoId: string | null
  /** Insurance, inspection and other deadlines past or within 30 days (or 1,000 km). */
  documentAlerts: number
  defaultDriverName: string | null
}

/** A deadline besides insurance and inspection: road tax, tachograph, service… */
export interface VehicleExpiration {
  id: string
  title: string
  /** yyyy-MM-dd */
  dueOn: string | null
  dueKm: number | null
  notes: string | null
}

export interface VehicleExpirationRequest {
  title: string
  dueOn?: string | null
  dueKm?: number | null
  notes?: string | null
}

export interface VehicleRefuel {
  id: string
  /** yyyy-MM-dd */
  date: string
  odometerKm: number
  liters: number
  cost: number | null
  station: string | null
  driverId: string | null
  driverName: string | null
}

export interface VehicleRefuelRequest {
  date: string
  odometerKm: number
  liters: number
  cost?: number | null
  station?: string | null
  driverId?: string | null
}

export interface VehicleLog {
  expirations: VehicleExpiration[]
  refuels: VehicleRefuel[]
  mileageKm: number
  distanceKm: number | null
  totalLiters: number
  totalCost: number
  /** Litres per 100 km; null with fewer than two readings. */
  averageConsumption: number | null
}
