import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  untracked,
} from '@angular/core'
import { toSignal } from '@angular/core/rxjs-interop'
import {
  FormBuilder,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms'
import { finalize, Observable } from 'rxjs'
import {
  LatLng,
  MapMarker,
  MapZone,
  MeetingPoint,
  PolygonPoint,
  Zone,
  ZoneRequest,
  ZoneShape,
  ZoneType,
} from '@/core/models'
import { AdminZonesService } from '@/core/services/admin-zones.service'
import { GeoService } from '@/core/services/geo.service'
import { problemMessage } from '@/core/utils/problem'
import { ButtonComponent } from '@/shared/components/button/button.component'
import { FormFieldComponent } from '@/shared/components/form-field/form-field.component'
import { IconComponent } from '@/shared/components/icon/icon.component'
import { MapComponent } from '@/shared/components/map/map.component'
import { ZONE_TYPES } from '../zone-labels'

type MeetingPointForm = FormGroup<{
  name: FormControl<string>
  address: FormControl<string>
  latitude: FormControl<number | null>
  longitude: FormControl<number | null>
  clientInstructions: FormControl<string>
  driverInstructions: FormControl<string>
}>

/** What the next map click does: edit the shape, or place meeting point n. */
type ClickMode = 'shape' | number

@Component({
  selector: 'app-zone-editor',
  imports: [
    ReactiveFormsModule,
    ButtonComponent,
    FormFieldComponent,
    IconComponent,
    MapComponent,
  ],
  templateUrl: './zone-editor.component.html',
  styleUrl: './zone-editor.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ZoneEditorComponent {
  /** null creates a new zone. */
  readonly zone = input<Zone | null>(null)
  readonly otherZones = input<Zone[]>([])
  readonly saved = output<Zone>()
  readonly deleted = output<string>()
  readonly closed = output()

  private readonly api = inject(AdminZonesService)
  private readonly geo = inject(GeoService)
  private readonly fb = inject(FormBuilder).nonNullable

  protected readonly types = ZONE_TYPES
  protected readonly saving = signal(false)
  protected readonly error = signal<string | null>(null)
  protected readonly confirmDelete = signal(false)
  protected readonly clickMode = signal<ClickMode>('shape')
  protected readonly fitPoints = signal<LatLng[]>([])

  protected readonly form = this.fb.group({
    name: ['', [Validators.required, Validators.maxLength(120)]],
    type: this.fb.control<ZoneType>('LimitedTraffic'),
    shape: this.fb.control<ZoneShape>('Circle'),
    centerLatitude: this.fb.control<number | null>(null),
    centerLongitude: this.fb.control<number | null>(null),
    radiusMeters: this.fb.control<number | null>(1000, [
      Validators.min(50),
      Validators.max(50000),
    ]),
    polygon: this.fb.control<PolygonPoint[]>([]),
    accessFee: [0, [Validators.min(0), Validators.max(500)]],
    isActive: [true],
    meetingPoints: this.fb.array<MeetingPointForm>([]),
  })

  private readonly value = toSignal(this.form.valueChanges, {
    initialValue: this.form.getRawValue(),
  })

  protected readonly typeHint = computed(
    () => ZONE_TYPES.find(t => t.value === this.value().type)?.hint ?? ''
  )

  protected readonly mapZones = computed<MapZone[]>(() => {
    const v = this.form.getRawValue()
    this.value()
    const others = this.otherZones()
      .filter(z => z.id !== this.zone()?.id)
      .map(zone => ({ zone }))
    return [
      ...others,
      {
        zone: {
          name: v.name || 'New zone',
          type: v.type,
          shape: v.shape,
          centerLatitude: v.centerLatitude,
          centerLongitude: v.centerLongitude,
          radiusMeters: v.radiusMeters,
          polygon: v.polygon,
        },
        highlighted: true,
      },
    ]
  })

  protected readonly markers = computed<MapMarker[]>(() => {
    this.value()
    const v = this.form.getRawValue()
    const markers: MapMarker[] = v.meetingPoints
      .filter(m => m.latitude !== null && m.longitude !== null)
      .map(m => ({
        position: [m.latitude!, m.longitude!],
        kind: 'meeting',
        label: m.name || 'Meeting point',
      }))
    if (v.shape === 'Polygon') {
      markers.push(
        ...v.polygon.map(p => ({
          position: [p.latitude, p.longitude] as LatLng,
          kind: 'vertex' as const,
        }))
      )
    } else if (v.centerLatitude !== null && v.centerLongitude !== null) {
      markers.push({
        position: [v.centerLatitude, v.centerLongitude],
        kind: 'vertex',
      })
    }
    return markers
  })

  protected readonly clickHint = computed(() => {
    const mode = this.clickMode()
    if (mode !== 'shape') {
      return `Click on the map to place meeting point ${mode + 1}.`
    }
    return this.value().shape === 'Circle'
      ? 'Click on the map to set the centre.'
      : 'Click on the map to add a corner; use “Undo” to remove the last one.'
  })

  constructor() {
    effect(() => {
      const zone = this.zone()
      untracked(() => this.load(zone))
    })
  }

  protected get meetingPoints() {
    return this.form.controls.meetingPoints
  }

  protected onMapClick([latitude, longitude]: LatLng): void {
    const mode = this.clickMode()
    if (mode !== 'shape') {
      const point = this.meetingPoints.at(mode)
      point.patchValue({
        latitude: round(latitude),
        longitude: round(longitude),
      })
      if (!point.controls.address.value) {
        this.geo.reverse(latitude, longitude).subscribe({
          next: place => point.controls.address.setValue(place.address),
          error: () => undefined,
        })
      }
      this.clickMode.set('shape')
      return
    }
    if (this.form.controls.shape.value === 'Circle') {
      this.form.patchValue({
        centerLatitude: round(latitude),
        centerLongitude: round(longitude),
      })
    } else {
      const polygon = this.form.controls.polygon
      polygon.setValue([
        ...polygon.value,
        { latitude: round(latitude), longitude: round(longitude) },
      ])
    }
    this.form.markAsDirty()
  }

  protected undoPoint(): void {
    const polygon = this.form.controls.polygon
    polygon.setValue(polygon.value.slice(0, -1))
  }

  protected clearPolygon(): void {
    this.form.controls.polygon.setValue([])
  }

  protected addMeetingPoint(): void {
    this.meetingPoints.push(this.meetingPointGroup())
    this.clickMode.set(this.meetingPoints.length - 1)
  }

  protected removeMeetingPoint(index: number): void {
    this.meetingPoints.removeAt(index)
    this.clickMode.set('shape')
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched()
      return
    }
    const v = this.form.getRawValue()
    const request: ZoneRequest = {
      ...v,
      name: v.name.trim(),
      meetingPoints: v.meetingPoints.map((m): MeetingPoint => ({
        name: m.name.trim(),
        address: m.address.trim(),
        latitude: m.latitude ?? 0,
        longitude: m.longitude ?? 0,
        clientInstructions: m.clientInstructions.trim() || null,
        driverInstructions: m.driverInstructions.trim() || null,
      })),
    }
    const zone = this.zone()
    this.run(
      zone ? this.api.update(zone.id, request) : this.api.create(request),
      saved => {
        this.form.markAsPristine()
        this.saved.emit(saved)
      }
    )
  }

  protected remove(): void {
    const zone = this.zone()
    if (zone) {
      this.run(this.api.delete(zone.id), () => this.deleted.emit(zone.id))
    }
  }

  private load(zone: Zone | null): void {
    this.error.set(null)
    this.confirmDelete.set(false)
    this.clickMode.set('shape')
    this.meetingPoints.clear()
    for (const point of zone?.meetingPoints ?? []) {
      this.meetingPoints.push(this.meetingPointGroup(point))
    }
    this.form.reset({
      name: zone?.name ?? '',
      type: zone?.type ?? 'LimitedTraffic',
      shape: zone?.shape ?? 'Circle',
      centerLatitude: zone?.centerLatitude ?? null,
      centerLongitude: zone?.centerLongitude ?? null,
      radiusMeters: zone?.radiusMeters ?? 1000,
      polygon: zone?.polygon ?? [],
      accessFee: zone?.accessFee ?? 0,
      isActive: zone?.isActive ?? true,
    })
    this.fitPoints.set(zone ? zonePoints(zone) : [])
  }

  private meetingPointGroup(point?: MeetingPoint): MeetingPointForm {
    return this.fb.group({
      name: [
        point?.name ?? '',
        [Validators.required, Validators.maxLength(120)],
      ],
      address: [point?.address ?? '', Validators.required],
      latitude: this.fb.control<number | null>(
        point?.latitude ?? null,
        Validators.required
      ),
      longitude: this.fb.control<number | null>(
        point?.longitude ?? null,
        Validators.required
      ),
      clientInstructions: [point?.clientInstructions ?? ''],
      driverInstructions: [point?.driverInstructions ?? ''],
    })
  }

  private run<T>(call: Observable<T>, done: (value: T) => void): void {
    this.saving.set(true)
    this.error.set(null)
    call.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: done,
      error: err => this.error.set(problemMessage(err)),
    })
  }
}

function round(value: number): number {
  return Math.round(value * 1e5) / 1e5
}

function zonePoints(zone: Zone): LatLng[] {
  const points: LatLng[] = zone.meetingPoints.map(m => [
    m.latitude,
    m.longitude,
  ])
  if (zone.shape === 'Polygon') {
    points.push(...zone.polygon.map(p => [p.latitude, p.longitude] as LatLng))
  } else if (
    zone.centerLatitude !== null &&
    zone.centerLongitude !== null &&
    zone.radiusMeters
  ) {
    // Roughly the circle's bounding box.
    const dLat = zone.radiusMeters / 111_000
    const dLng = dLat / Math.cos((zone.centerLatitude * Math.PI) / 180)
    points.push(
      [zone.centerLatitude - dLat, zone.centerLongitude - dLng],
      [zone.centerLatitude + dLat, zone.centerLongitude + dLng]
    )
  }
  return points
}
