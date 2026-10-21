import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  effect,
  ElementRef,
  inject,
  input,
  output,
  untracked,
  viewChild,
  ViewEncapsulation,
} from '@angular/core'
import * as L from 'leaflet'
import { LatLng, MapMarker, MapZone, ZoneType } from '@/core/models'
import { MAP_CONFIG } from '@/core/tokens/map-config.token'

const ZONE_MODIFIER: Record<ZoneType, string> = {
  NoCarAccess: 'no-car',
  LimitedTraffic: 'ztl',
  Airport: 'hub',
  TrainStation: 'hub',
  Port: 'hub',
}

/** Leaflet map with markers, a route line and zone shapes. Click events are emitted for editors. */
@Component({
  selector: 'app-map',
  template: '<div #canvas class="map__canvas"></div>',
  styleUrl: './map.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  // Leaflet creates its own DOM, so the BEM styles for markers and shapes must be global.
  encapsulation: ViewEncapsulation.None,
  host: {
    class: 'map',
    '[class.map--clickable]': 'clickable()',
    '[class.map--locked]': 'locked()',
  },
})
export class MapComponent {
  readonly markers = input<MapMarker[]>([])
  readonly path = input<LatLng[]>([])
  readonly zones = input<MapZone[]>([])
  /** The view zooms to these points whenever the array changes. */
  readonly fitPoints = input<LatLng[]>([])
  readonly clickable = input(false)
  /** A picture of the route: no dragging, zooming or scroll capture (set once, at creation). */
  readonly locked = input(false)
  readonly mapClick = output<LatLng>()

  private readonly config = inject(MAP_CONFIG)
  private readonly canvas =
    viewChild.required<ElementRef<HTMLElement>>('canvas')
  private map?: L.Map
  private readonly layers = L.layerGroup()

  constructor() {
    afterNextRender(() => {
      const interactive = !untracked(this.locked)
      this.map = L.map(this.canvas().nativeElement, {
        center: this.config.center,
        zoom: this.config.zoom,
        zoomControl: interactive,
        attributionControl: true,
        dragging: interactive,
        scrollWheelZoom: interactive,
        touchZoom: interactive,
        doubleClickZoom: interactive,
        boxZoom: interactive,
        keyboard: interactive,
      })
      L.tileLayer(this.config.tileUrl, {
        attribution: this.config.attribution,
        className: 'map__tiles',
        maxZoom: 19,
      }).addTo(this.map)
      this.layers.addTo(this.map)
      this.map.on('click', (e: L.LeafletMouseEvent) => {
        if (this.clickable()) {
          this.mapClick.emit([e.latlng.lat, e.latlng.lng])
        }
      })
      // The container may still be resizing when the map is created.
      setTimeout(() => this.map?.invalidateSize())
      this.draw()
      this.fit(untracked(this.fitPoints))
    })

    effect(() => {
      this.markers()
      this.path()
      this.zones()
      this.draw()
    })
    effect(() => this.fit(this.fitPoints()))

    inject(DestroyRef).onDestroy(() => this.map?.remove())
  }

  private draw(): void {
    if (!this.map) {
      return
    }
    this.layers.clearLayers()

    for (const { zone, highlighted } of untracked(this.zones)) {
      const className = [
        'map__zone',
        `map__zone--${ZONE_MODIFIER[zone.type]}`,
        highlighted ? 'map__zone--highlighted' : '',
      ].join(' ')
      if (
        zone.shape === 'Circle' &&
        zone.centerLatitude !== null &&
        zone.centerLongitude !== null &&
        zone.radiusMeters
      ) {
        L.circle([zone.centerLatitude, zone.centerLongitude], {
          radius: zone.radiusMeters,
          className,
        })
          .bindTooltip(zone.name)
          .addTo(this.layers)
      } else if (zone.shape === 'Polygon' && zone.polygon.length > 1) {
        L.polygon(
          zone.polygon.map(p => [p.latitude, p.longitude] as LatLng),
          { className }
        )
          .bindTooltip(zone.name)
          .addTo(this.layers)
      }
    }

    const path = untracked(this.path)
    if (path.length > 1) {
      L.polyline(path, { className: 'map__route' }).addTo(this.layers)
    }

    for (const marker of untracked(this.markers)) {
      const icon = L.divIcon({
        className: `map-marker map-marker--${marker.kind}`,
        html: '<span class="map-marker__dot"></span>',
        iconSize: marker.kind === 'vertex' ? [12, 12] : [22, 22],
      })
      const layer = L.marker(marker.position, {
        icon,
        keyboard: false,
        interactive: !!marker.label,
      })
      if (marker.label) {
        layer.bindTooltip(marker.label, { direction: 'top', offset: [0, -10] })
      }
      layer.addTo(this.layers)
    }
  }

  private fit(points: LatLng[]): void {
    if (!this.map || points.length === 0) {
      return
    }
    if (points.length === 1) {
      this.map.setView(points[0], 14)
      return
    }
    this.map.fitBounds(L.latLngBounds(points), {
      padding: [32, 32],
      maxZoom: 15,
    })
  }
}
