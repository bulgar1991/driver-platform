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
  signal,
  untracked,
  viewChild,
  ViewEncapsulation,
} from '@angular/core'
import type * as Leaflet from 'leaflet'
import { LatLng, MapMarker, MapZone, ZoneType } from '@/core/models'
import { MAP_CONFIG } from '@/core/tokens/map-config.token'
import { CarLoaderComponent } from '@/shared/components/car-loader/car-loader.component'

/** The loader gives way after this even if some tiles never arrive (offline, blocked). */
const TILES_TIMEOUT_MS = 8000
/** How long a "two fingers" / "Ctrl + scroll" hint stays after the last gesture that needs it. */
const HINT_MS = 1500
/** Wheel distance for one zoom step with Ctrl / ⌘ (a mouse notch is about 100, a trackpad less). */
const WHEEL_STEP = 100

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
  imports: [CarLoaderComponent],
  template: `<div #canvas class="map__canvas"></div>
    @if (loading()) {
      <div class="map__loading">
        <app-car-loader [label]="loadingLabel" />
      </div>
    }
    @if (hint(); as text) {
      <p class="map__hint" aria-hidden="true">{{ text }}</p>
    }`,
  styleUrl: './map.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  // Leaflet creates its own DOM, so the BEM styles for markers and shapes must be global.
  encapsulation: ViewEncapsulation.None,
  host: {
    class: 'map',
    '[class.map--clickable]': 'clickable()',
    '[class.map--locked]': 'locked()',
    '[class.map--cooperative]': 'cooperative()',
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
  /**
   * A map that can be moved without getting in the way of the page (set once, at creation):
   * with a mouse it drags, and the wheel zooms once the map is in use (dragged, clicked, + / −)
   * or with Ctrl / ⌘; on touch screens one finger scrolls the page and two move and zoom the map.
   */
  readonly cooperative = input(false)
  /** + / − buttons even on a locked map (zooming without dragging or scroll capture). */
  readonly zoomButtons = input(false)
  /** Zoom used when there is a single point to show (14: streets; 12: the whole city). */
  readonly singlePointZoom = input(14)
  readonly mapClick = output<LatLng>()

  /** Until the first tiles are drawn: the car over the whole map instead of an empty dark box. */
  protected readonly loading = signal(true)
  protected readonly loadingLabel = $localize`Loading the map…`
  protected readonly hint = signal<string | null>(null)
  private hintTimer?: ReturnType<typeof setTimeout>
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef)
  private readonly listeners = new AbortController()
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
      this.listeners.abort()
      clearTimeout(this.hintTimer)
      this.map?.remove()
    })
  }

  private create(L: typeof Leaflet): void {
    this.L = L
    const cooperative = untracked(this.cooperative)
    const free = !untracked(this.locked) && !cooperative
    const interactive = free || cooperative
    this.map = L.map(this.canvas().nativeElement, {
      center: this.config.center,
      zoom: this.config.zoom,
      zoomControl: false,
      attributionControl: true,
      dragging: interactive,
      scrollWheelZoom: free,
      touchZoom: interactive,
      doubleClickZoom: interactive,
      boxZoom: free,
      keyboard: interactive,
    })
    if (interactive || untracked(this.zoomButtons)) {
      L.control
        .zoom({ position: free ? 'topleft' : 'bottomright' })
        .addTo(this.map)
    }
    if (cooperative) {
      this.cooperate(this.map)
    }
    const tiles = L.tileLayer(this.config.tileUrl, {
      attribution: this.config.attribution,
      className: 'map__tiles',
      maxZoom: 19,
    }).addTo(this.map)
    const loaded = () => this.loading.set(false)
    tiles.once('load', loaded)
    setTimeout(loaded, TILES_TIMEOUT_MS)
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

  /** The gestures of a cooperative map: see `cooperative`. */
  private cooperate(map: Leaflet.Map): void {
    const host = this.host.nativeElement
    const signal = this.listeners.signal
    const mac = /Mac|iPhone|iPad/.test(navigator.userAgent)
    const hints = {
      touch: $localize`Use two fingers to move the map`,
      wheel: mac
        ? $localize`Use ⌘ + scroll to zoom the map`
        : $localize`Use Ctrl + scroll to zoom the map`,
    }

    // Before Leaflet sees the press (capture on the host): a finger never drags the map, so the
    // page scrolls under it; two fingers pinch and move it (Leaflet's touch zoom). A mouse drags,
    // and from then on the wheel zooms, until the pointer leaves the map.
    host.addEventListener(
      'pointerdown',
      e => {
        if (e.pointerType === 'touch') {
          map.dragging.disable()
          return
        }
        map.dragging.enable()
        map.scrollWheelZoom.enable()
        this.showHint(null)
      },
      { capture: true, signal }
    )
    host.addEventListener('mouseleave', () => map.scrollWheelZoom.disable(), {
      signal,
    })
    host.addEventListener(
      'touchmove',
      e => {
        if (e.touches.length === 1) {
          this.showHint(hints.touch)
        }
      },
      { passive: true, signal }
    )

    // Ctrl / ⌘ + wheel (and a trackpad pinch, which reports as one) zooms right away; a plain
    // wheel scrolls the page until the map is in use, and says how to zoom.
    let wheel = 0
    host.addEventListener(
      'wheel',
      e => {
        if (map.scrollWheelZoom.enabled()) {
          return
        }
        if (!e.ctrlKey && !e.metaKey) {
          this.showHint(hints.wheel)
          return
        }
        e.preventDefault()
        this.showHint(null)
        wheel += e.deltaY
        if (Math.abs(wheel) >= WHEEL_STEP) {
          const zoom = map.getZoom() + (wheel < 0 ? 1 : -1)
          wheel = 0
          map.setZoomAround(map.mouseEventToContainerPoint(e), zoom)
        }
      },
      { passive: false, signal }
    )
  }

  private showHint(text: string | null): void {
    clearTimeout(this.hintTimer)
    this.hint.set(text)
    if (text) {
      this.hintTimer = setTimeout(() => this.hint.set(null), HINT_MS)
    }
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
