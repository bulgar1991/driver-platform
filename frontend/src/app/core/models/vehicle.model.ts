/** Sedan 1–3, Estate/SUV 1–4, Van 5–8 passengers. */
export type VehicleCategory = 'Sedan' | 'Estate' | 'Van'

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
  documentAlerts: number
}
