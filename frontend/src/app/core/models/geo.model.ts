export interface GeoPoint {
  address: string
  latitude: number
  longitude: number
}

/** A text typed in English in the admin panel, in the other site languages. */
export interface Translations {
  it?: string | null
  de?: string | null
  fr?: string | null
}

/** A place reachable by car where the client is picked up or dropped off. */
export interface MeetingPoint extends GeoPoint {
  name: string
  /** In English; clients read {@link clientInstructionsTranslations} in their language. */
  clientInstructions?: string | null
  clientInstructionsTranslations?: Translations | null
  driverInstructions?: string | null
}

export type PlaceKind = 'MeetingPoint' | 'Address'

/** An address search result. */
export interface Place extends GeoPoint {
  label: string
  kind: PlaceKind
}
