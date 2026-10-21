import { Zone } from './zone.model'

/** [latitude, longitude] */
export type LatLng = [number, number]

export type MapMarkerKind = 'pickup' | 'dropoff' | 'meeting' | 'vertex'

export interface MapMarker {
  position: LatLng
  kind: MapMarkerKind
  label?: string
}

export interface MapZone {
  zone: Pick<
    Zone,
    | 'shape'
    | 'centerLatitude'
    | 'centerLongitude'
    | 'radiusMeters'
    | 'polygon'
    | 'type'
    | 'name'
  >
  highlighted?: boolean
}
