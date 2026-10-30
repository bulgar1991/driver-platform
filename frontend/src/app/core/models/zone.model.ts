import { VehicleCategory } from './vehicle.model'
import { MeetingPoint, Translations } from './geo.model'

export type ZoneType =
  'NoCarAccess' | 'LimitedTraffic' | 'Airport' | 'TrainStation' | 'Port'

export type ZoneShape = 'Circle' | 'Polygon'

export interface PolygonPoint {
  latitude: number
  longitude: number
}

/** A circle or polygon on the map with local rules (Venice, ZTL, airports). */
/** How rides starting or ending in a zone are priced. */
export type ZonePricing = 'Standard' | 'FixedPrice' | 'PerKm'

export interface ZoneRate {
  category: VehicleCategory
  fixedPrice?: number | null
  baseFee?: number | null
  pricePerKm?: number | null
}

export interface Zone {
  id: string
  /** In the client's language from the public API; in English in the admin API. */
  name: string
  /** Only present in the admin API. */
  nameTranslations?: Translations | null
  type: ZoneType
  shape: ZoneShape
  centerLatitude: number | null
  centerLongitude: number | null
  radiusMeters: number | null
  polygon: PolygonPoint[]
  accessFee: number
  /** Only present in the admin API. */
  pricing?: ZonePricing
  /** Per vehicle category; a category left out uses the normal rate. Admin API only. */
  rates?: ZoneRate[]
  /** A fixed price only applies up to this many km. Admin API only. */
  fixedPriceMaxKm?: number | null
  meetingPoints: MeetingPoint[]
  /** Only present in the admin API. */
  isActive?: boolean
}

export type ZoneRequest = Omit<Zone, 'id'> & { isActive: boolean }
