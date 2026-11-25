import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core'
import { MAT_DIALOG_DATA, MatDialogClose } from '@angular/material/dialog'
import { GeoPoint, LatLng, MapMarker, MapMarkerKind } from '@/core/models'
import { GeoService } from '@/core/services/geo.service'
import { ButtonComponent } from '@/shared/components/button/button.component'
import { DialogComponent } from '@/shared/components/dialog/dialog.component'
import { IconComponent } from '@/shared/components/icon/icon.component'
import { MapComponent } from '@/shared/components/map/map.component'

export interface MapPickerData {
  title: string
  kind: MapMarkerKind
  value: GeoPoint | null
}

/** Pick an address by clicking the map (the street address of the point); closes with the GeoPoint. */
@Component({
  selector: 'app-map-picker-dialog',
  imports: [
    DialogComponent,
    ButtonComponent,
    MatDialogClose,
    MapComponent,
    IconComponent,
  ],
  template: `
    <app-dialog [title]="data.title">
      <p class="map-picker__hint">
        <app-icon name="info" />
        <ng-container i18n
          >Click on the map where the ride starts or ends.</ng-container
        >
      </p>
      <app-map
        class="map-picker__map"
        [cooperative]="true"
        [clickable]="true"
        [zoomButtons]="true"
        [singlePointZoom]="16"
        [markers]="markers()"
        [fitPoints]="fitPoints"
        (mapClick)="pick($event)" />
      <p class="map-picker__address" aria-live="polite">
        @if (locating()) {
          <ng-container i18n>Finding the address…</ng-container>
        } @else if (error()) {
          <span class="map-picker__error">{{ error() }}</span>
        } @else if (point(); as p) {
          <app-icon name="pin" /> {{ p.address }}
        }
      </p>
      <div dialog-actions>
        <button appButton variant="ghost" type="button" mat-dialog-close
          ><ng-container i18n>Cancel</ng-container></button
        >
        <button
          appButton
          type="button"
          [disabled]="!point() || locating()"
          [mat-dialog-close]="point()"
          ><ng-container i18n>Use this address</ng-container></button
        >
      </div>
    </app-dialog>
  `,
  styles: `
    .map-picker__hint {
      @apply mb-3 flex items-center gap-2 text-sm text-ivory-400;
      app-icon {
        @apply h-4 w-4 shrink-0;
      }
    }
    .map-picker__map {
      @apply block h-[60vh] min-h-[18rem] w-full overflow-hidden rounded-sm border border-ink-600;
    }
    .map-picker__address {
      @apply mt-3 flex min-h-6 items-center gap-2 text-sm text-ivory;
      app-icon {
        @apply h-4 w-4 shrink-0 text-gold;
      }
    }
    .map-picker__error {
      @apply text-danger;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MapPickerDialogComponent {
  protected readonly data = inject<MapPickerData>(MAT_DIALOG_DATA)
  private readonly geo = inject(GeoService)

  protected readonly point = signal<GeoPoint | null>(this.data.value)
  protected readonly locating = signal(false)
  protected readonly error = signal<string | null>(null)
  /** Zooms to the current address once, when the popup opens. */
  protected readonly fitPoints: LatLng[] = this.data.value
    ? [[this.data.value.latitude, this.data.value.longitude]]
    : []
  protected readonly markers = computed<MapMarker[]>(() => {
    const p = this.point()
    return p
      ? [{ position: [p.latitude, p.longitude], kind: this.data.kind }]
      : []
  })

  protected pick([latitude, longitude]: LatLng): void {
    this.locating.set(true)
    this.error.set(null)
    this.geo.reverse(latitude, longitude).subscribe({
      next: place => {
        this.locating.set(false)
        this.point.set({
          address: place.address,
          latitude: place.latitude,
          longitude: place.longitude,
        })
      },
      error: () => {
        this.locating.set(false)
        this.error.set(
          $localize`No address found there. Try a point on a street in a country we serve.`
        )
      },
    })
  }
}
