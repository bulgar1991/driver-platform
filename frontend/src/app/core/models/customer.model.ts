import { BookingSource, BookingStatus } from './booking.model'
import { VehicleCategory } from './vehicle.model'

export type CustomerFilter =
  'All' | 'Direct' | 'ViaPartner' | 'Upcoming' | 'Returning' | 'Marketing'
export type CustomerSort = 'LastRide' | 'Name' | 'Rides' | 'Spent'

export interface CustomerQuery {
  search: string
  filter: CustomerFilter
  sort: CustomerSort
  page: number
  pageSize: number
}

export interface CustomerListItem {
  id: string
  name: string
  email: string | null
  phone: string
  language: string
  marketingConsent: boolean
  hasNotes: boolean
  partnerName: string | null
  rides: number
  cancelled: number
  spent: number
  lastRide: string | null
  nextRide: string | null
}

export interface CustomerSummary {
  customers: number
  returning: number
  upcoming: number
  spent: number
}

export interface CustomerList {
  items: CustomerListItem[]
  total: number
  page: number
  pageSize: number
  summary: CustomerSummary
  currency: string
}

export interface CustomerRide {
  id: string
  reference: string
  status: BookingStatus
  source: BookingSource
  pickupLocal: string
  pickupLabel: string
  dropoffLabel: string
  vehicleCategory: VehicleCategory
  totalPrice: number
  partnerName: string | null
  driverName: string | null
}

export interface CustomerDetail {
  id: string
  firstName: string
  lastName: string
  email: string | null
  phone: string
  language: string
  marketingConsent: boolean
  gdprConsentAt: string | null
  notesForDriver: string | null
  createdAt: string
  rides: number
  cancelled: number
  spent: number
  averageRide: number | null
  partners: string[]
  history: CustomerRide[]
  currency: string
}

export interface CustomerRequest {
  firstName: string
  lastName: string
  email: string | null
  phone: string
  language: string
  marketingConsent: boolean
  notesForDriver: string | null
}
