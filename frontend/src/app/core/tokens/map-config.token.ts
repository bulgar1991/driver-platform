import { InjectionToken } from '@angular/core'
import { LatLng } from '../models'

export interface MapConfig {
  tileUrl: string
  attribution: string
  /** Initial view: the whole of Italy. */
  center: LatLng
  zoom: number
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
    center: [42.5, 12.5],
    zoom: 6,
  }),
})
