/** Shown as Standard 1–3, Business 1–3, VIP (SUV) 1–4 and Bus 5–8 passengers; cheapest first. */
export type VehicleCategory = 'Sedan' | 'Business' | 'Estate' | 'Van'

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
