import { MeetingPoint } from './geo.model'

export type ZoneType =
  'NoCarAccess' | 'LimitedTraffic' | 'Airport' | 'TrainStation' | 'Port'

export type ZoneShape = 'Circle' | 'Polygon'

export interface PolygonPoint {
  latitude: number
  longitude: number
}

/** A circle or polygon on the map with local rules (Venice, ZTL, airports). */
export interface Zone {
  id: string
  name: string
  type: ZoneType
  shape: ZoneShape
  centerLatitude: number | null
  centerLongitude: number | null
  radiusMeters: number | null
  polygon: PolygonPoint[]
  accessFee: number
  meetingPoints: MeetingPoint[]
  /** Only present in the admin API. */
  isActive?: boolean
}

export type ZoneRequest = Omit<Zone, 'id'> & { isActive: boolean }
