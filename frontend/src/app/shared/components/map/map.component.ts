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
import type * as Leaflet from 'leaflet'
import { LatLng, MapMarker, MapZone, ZoneType } from '@/core/models'
import { MAP_CONFIG } from '@/core/tokens/map-config.token'

const ZONE_MODIFIER: Record<ZoneType, string> = {
  NoCarAccess: 'no-car',
  LimitedTraffic: 'ztl',
  Airport: 'hub',
  TrainStation: 'hub',
  Port: 'hub',
}

/**
 * Leaflet map with markers, a route line and zone shapes. Click events are emitted for editors.
 * Leaflet is loaded only in the browser, after the first render: it cannot run while prerendering.
 */
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
  /** + / − buttons even on a locked map (zooming without dragging or scroll capture). */
  readonly zoomButtons = input(false)
  /** Zoom used when there is a single point to show (14: streets; 12: the whole city). */
  readonly singlePointZoom = input(14)
  readonly mapClick = output<LatLng>()

  private readonly config = inject(MAP_CONFIG)
  private readonly canvas =
    viewChild.required<ElementRef<HTMLElement>>('canvas')
  private L?: typeof Leaflet
  private map?: Leaflet.Map
  private layers?: Leaflet.LayerGroup
  private destroyed = false

  constructor() {
    afterNextRender(() => {
      void import('leaflet').then(module => {
        if (!this.destroyed) {
          // CommonJS package: the API is the default export once bundled.
          this.create(
            (module as { default?: typeof Leaflet }).default ?? module
          )
        }
      })
    })

    effect(() => {
      this.markers()
      this.path()
      this.zones()
      this.draw()
    })
    effect(() => this.fit(this.fitPoints()))

    inject(DestroyRef).onDestroy(() => {
      this.destroyed = true
      this.map?.remove()
    })
  }

  private create(L: typeof Leaflet): void {
    this.L = L
    const interactive = !untracked(this.locked)
    this.map = L.map(this.canvas().nativeElement, {
      center: this.config.center,
      zoom: this.config.zoom,
      zoomControl: false,
      attributionControl: true,
      dragging: interactive,
      scrollWheelZoom: interactive,
      touchZoom: interactive,
      doubleClickZoom: interactive,
      boxZoom: interactive,
      keyboard: interactive,
    })
    if (interactive || untracked(this.zoomButtons)) {
      L.control
        .zoom({ position: interactive ? 'topleft' : 'bottomright' })
        .addTo(this.map)
    }
    L.tileLayer(this.config.tileUrl, {
      attribution: this.config.attribution,
      className: 'map__tiles',
      maxZoom: 19,
    }).addTo(this.map)
    this.layers = L.layerGroup().addTo(this.map)
    // Without dragging, Leaflet reports the release of a drag as a click: only a press and release
    // at (nearly) the same spot counts.
    let pressedAt: Leaflet.Point | null = null
    this.map.on(
      'mousedown',
      (e: Leaflet.LeafletMouseEvent) => (pressedAt = e.containerPoint)
    )
    this.map.on('click', (e: Leaflet.LeafletMouseEvent) => {
      const moved = pressedAt
        ? pressedAt.distanceTo(e.containerPoint) > 5
        : false
      if (this.clickable() && !moved) {
        this.mapClick.emit([e.latlng.lat, e.latlng.lng])
      }
    })
    // The container may still be resizing when the map is created.
    setTimeout(() => this.map?.invalidateSize())
    this.draw()
    // Nothing to show yet: the whole service area, as large as the map allows.
    if (untracked(this.fitPoints).length === 0) {
      this.map.fitBounds(L.latLngBounds(this.config.area), { padding: [8, 8] })
    }
    this.fit(untracked(this.fitPoints))
  }

  private draw(): void {
    const { L, map, layers } = this
    if (!L || !map || !layers) {
      return
    }
    layers.clearLayers()

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
          .addTo(layers)
      } else if (zone.shape === 'Polygon' && zone.polygon.length > 1) {
        L.polygon(
          zone.polygon.map(p => [p.latitude, p.longitude] as LatLng),
          { className }
        )
          .bindTooltip(zone.name)
          .addTo(layers)
      }
    }

    const path = untracked(this.path)
    if (path.length > 1) {
      L.polyline(path, { className: 'map__route' }).addTo(layers)
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
      layer.addTo(layers)
    }
  }

  private fit(points: LatLng[]): void {
    if (!this.L || !this.map || points.length === 0) {
      return
    }
    if (points.length === 1) {
      this.map.setView(points[0], untracked(this.singlePointZoom))
      return
    }
    this.map.fitBounds(this.L.latLngBounds(points), {
      padding: [32, 32],
      maxZoom: 15,
    })
  }
}
