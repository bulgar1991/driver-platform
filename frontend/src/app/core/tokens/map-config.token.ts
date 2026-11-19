import { InjectionToken } from '@angular/core'
import { LatLng } from '../models'
import { AREA_BOUNDS, AREA_CENTER } from '../area'

export interface MapConfig {
  tileUrl: string
  attribution: string
  /** Fallback view before the map has a size. */
  center: LatLng
  zoom: number
  /** Initial view: centred here… */
  focus: LatLng
  /** …at the zoom that fits this box in the map… */
  area: [LatLng, LatLng]
  /** …but never further out than this. */
  minAreaZoom: number
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
    center: AREA_CENTER,
    zoom: 6,
    focus: AREA_CENTER,
    area: AREA_BOUNDS,
    minAreaZoom: 6,
  }),
})
