import { InjectionToken } from '@angular/core'
import { LatLng } from '../models'
import { AREA_BOUNDS } from '../area'

export interface MapConfig {
  tileUrl: string
  attribution: string
  /** Fallback view before the map has a size. */
  center: LatLng
  zoom: number
  /** Initial view, fitted to the map's size: the area served (SERVICE_AREA). */
  area: [LatLng, LatLng]
}

/**
 * OpenStreetMap tiles, darkened in CSS to match the theme (see .map__tiles).
 * The public OSM servers suit development and low traffic; production should use a tile provider.
 */
export const MAP_CONFIG = new InjectionToken<MapConfig>('MAP_CONFIG', {
  providedIn: 'root',
  factory: () => ({
    tileUrl: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
    attribution:
      '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
    center: [46, 9],
    zoom: 5,
    area: AREA_BOUNDS,
  }),
})
