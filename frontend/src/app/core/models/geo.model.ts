export interface GeoPoint {
  address: string
  latitude: number
  longitude: number
}

/** A place reachable by car where the client is picked up or dropped off. */
export interface MeetingPoint extends GeoPoint {
  name: string
  clientInstructions?: string | null
  driverInstructions?: string | null
}

export type PlaceKind = 'MeetingPoint' | 'Address'

/** An address search result. */
export interface Place extends GeoPoint {
  label: string
  kind: PlaceKind
}
