import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core'
import { finalize } from 'rxjs'
import { LatLng, MapZone, Zone } from '@/core/models'
import { AdminZonesService } from '@/core/services/admin-zones.service'
import { problemMessage } from '@/core/utils/problem'
import { ButtonComponent } from '@/shared/components/button/button.component'
import { IconComponent } from '@/shared/components/icon/icon.component'
import { MapComponent } from '@/shared/components/map/map.component'
import { MoneyPipe } from '@/shared/pipes/money.pipe'
import { ZoneEditorComponent } from './zone-editor/zone-editor.component'
import { ZONE_TYPE_LABEL } from './zone-labels'
import { zonePoints } from './zone-points'
import { TranslatedPipe } from '@/shared/pipes/translated.pipe'

/** Business plan §5: circles and polygons with local rules, configured without code. */
@Component({
  selector: 'app-zones',
  imports: [
    ButtonComponent,
    IconComponent,
    MapComponent,
    MoneyPipe,
    ZoneEditorComponent,
    TranslatedPipe,
  ],
  templateUrl: './zones.component.html',
  styleUrl: './zones.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ZonesComponent implements OnInit {
  private readonly api = inject(AdminZonesService)

  protected readonly typeLabel = ZONE_TYPE_LABEL
  protected readonly zones = signal<Zone[]>([])
  protected readonly loading = signal(true)
  protected readonly error = signal<string | null>(null)
  /** undefined: nothing open; null: a new zone. */
  protected readonly selected = signal<Zone | null | undefined>(undefined)

  protected readonly mapZones = computed<MapZone[]>(() =>
    this.zones().map(zone => ({ zone }))
  )
  /** The map can't be moved by hand, so it frames every zone. */
  protected readonly fitPoints = computed<LatLng[]>(() =>
    this.zones().flatMap(zonePoints)
  )

  ngOnInit(): void {
    this.api
      .list()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: zones => this.zones.set(zones),
        error: err => this.error.set(problemMessage(err)),
      })
  }

  protected onSaved(zone: Zone): void {
    this.zones.update(list => {
      const exists = list.some(z => z.id === zone.id)
      const next = exists
        ? list.map(z => (z.id === zone.id ? zone : z))
        : [...list, zone]
      return next.sort((a, b) => a.name.localeCompare(b.name))
    })
    this.selected.set(zone)
  }

  protected onDeleted(id: string): void {
    this.zones.update(list => list.filter(z => z.id !== id))
    this.selected.set(undefined)
  }
}
