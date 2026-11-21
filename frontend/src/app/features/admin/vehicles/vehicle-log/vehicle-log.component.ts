import { DatePipe, DecimalPipe } from '@angular/common'
import {
  ChangeDetectionStrategy,
  Component,
  effect,
  inject,
  input,
  output,
  signal,
} from '@angular/core'
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms'
import { MatError, MatFormField, MatLabel } from '@angular/material/form-field'
import { MatInput } from '@angular/material/input'
import { MatOption, MatSelect } from '@angular/material/select'
import { finalize, Observable } from 'rxjs'
import { downloadCsv } from '@/core/utils/csv'
import { Driver, Vehicle, VehicleExpiration, VehicleLog } from '@/core/models'
import { AdminFleetService } from '@/core/services/admin-fleet.service'
import { SITE_CURRENCY } from '@/core/currency'
import { todayInOffice } from '@/core/utils/local-time'
import { problemMessage } from '@/core/utils/problem'
import { ButtonComponent } from '@/shared/components/button/button.component'
import { IconComponent } from '@/shared/components/icon/icon.component'
import { ADMIN_TEXT } from '@/features/admin/admin-texts'

/** Under the vehicle list: the selected car's deadlines, and its refuellings with mileage and consumption. */
@Component({
  selector: 'app-vehicle-log',
  imports: [
    DatePipe,
    DecimalPipe,
    ReactiveFormsModule,
    MatFormField,
    MatLabel,
    MatError,
    MatInput,
    MatSelect,
    MatOption,
    ButtonComponent,
    IconComponent,
  ],
  templateUrl: './vehicle-log.component.html',
  styleUrl: './vehicle-log.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class VehicleLogComponent {
  readonly vehicle = input.required<Vehicle>()
  readonly drivers = input<Driver[]>([])
  /** Mileage or deadlines changed: the list's alerts need refreshing. */
  readonly changed = output<void>()

  protected readonly text = ADMIN_TEXT
  protected readonly currency = SITE_CURRENCY
  protected readonly titles = [
    $localize`Road tax`,
    $localize`Tachograph check`,
    $localize`Service`,
    $localize`Tyres`,
    $localize`Fire extinguisher`,
    $localize`First-aid kit`,
    $localize`Lease end`,
  ]

  private readonly api = inject(AdminFleetService)
  private readonly fb = inject(FormBuilder).nonNullable
  protected readonly today = todayInOffice()
  protected readonly soon = todayInOffice(30)

  protected readonly log = signal<VehicleLog | null>(null)
  protected readonly error = signal<string | null>(null)
  protected readonly busy = signal(false)
  protected readonly from = signal(todayInOffice(-365))
  protected readonly to = signal(todayInOffice())
  /** The deadline being edited; null while adding a new one. */
  protected readonly editingExpiration = signal<VehicleExpiration | null>(null)

  protected readonly expirationForm = this.fb.group({
    title: ['', [Validators.required, Validators.maxLength(80)]],
    dueOn: [''],
    dueKm: this.fb.control<number | null>(null, Validators.min(0)),
    notes: ['', Validators.maxLength(500)],
  })

  protected readonly refuelForm = this.fb.group({
    date: [todayInOffice(), Validators.required],
    odometerKm: this.fb.control<number | null>(null, [
      Validators.required,
      Validators.min(0),
    ]),
    liters: this.fb.control<number | null>(null, [
      Validators.required,
      Validators.min(0.1),
    ]),
    cost: this.fb.control<number | null>(null, Validators.min(0)),
    station: ['', Validators.maxLength(80)],
    driverId: this.fb.control<string | null>(null),
  })

  constructor() {
    effect(() => {
      const v = this.vehicle()
      this.cancelExpiration()
      this.refuelForm.reset({
        date: todayInOffice(),
        driverId: v.defaultDriverId ?? null,
        odometerKm: null,
        liters: null,
        cost: null,
        station: '',
      })
      this.load()
    })
  }

  protected setPeriod(from: string, to: string): void {
    if (from && to && from <= to) {
      this.from.set(from)
      this.to.set(to)
      this.load()
    }
  }

  protected expirationTone(e: VehicleExpiration): 'danger' | 'warning' | null {
    const km = this.log()?.mileageKm ?? this.vehicle().mileageKm
    if (
      (e.dueOn && e.dueOn < this.today) ||
      (e.dueKm !== null && e.dueKm <= km)
    ) {
      return 'danger'
    }
    if (
      (e.dueOn && e.dueOn <= this.soon) ||
      (e.dueKm !== null && e.dueKm <= km + 1000)
    ) {
      return 'warning'
    }
    return null
  }

  protected editExpiration(e: VehicleExpiration): void {
    this.editingExpiration.set(e)
    this.expirationForm.reset({
      title: e.title,
      dueOn: e.dueOn ?? '',
      dueKm: e.dueKm,
      notes: e.notes ?? '',
    })
  }

  protected cancelExpiration(): void {
    this.editingExpiration.set(null)
    this.expirationForm.reset({ title: '', dueOn: '', dueKm: null, notes: '' })
  }

  protected saveExpiration(): void {
    if (this.expirationForm.invalid) {
      this.expirationForm.markAllAsTouched()
      return
    }
    const v = this.expirationForm.getRawValue()
    this.run(
      this.api.saveExpiration(
        this.vehicle().id,
        {
          title: v.title.trim(),
          dueOn: v.dueOn || null,
          dueKm: v.dueKm,
          notes: v.notes.trim() || null,
        },
        this.editingExpiration()?.id
      ),
      () => this.cancelExpiration()
    )
  }

  protected deleteExpiration(e: VehicleExpiration): void {
    this.run(this.api.deleteExpiration(this.vehicle().id, e.id), () => {
      if (this.editingExpiration()?.id === e.id) {
        this.cancelExpiration()
      }
    })
  }

  protected addRefuel(): void {
    if (this.refuelForm.invalid) {
      this.refuelForm.markAllAsTouched()
      return
    }
    const v = this.refuelForm.getRawValue()
    this.run(
      this.api.addRefuel(this.vehicle().id, {
        date: v.date,
        odometerKm: v.odometerKm!,
        liters: v.liters!,
        cost: v.cost,
        station: v.station.trim() || null,
        driverId: v.driverId,
      }),
      () =>
        this.refuelForm.reset({
          date: v.date,
          driverId: v.driverId,
          odometerKm: null,
          liters: null,
          cost: null,
          station: v.station,
        })
    )
  }

  protected deleteRefuel(id: string): void {
    this.run(this.api.deleteRefuel(this.vehicle().id, id))
  }

  /** CSV for a spreadsheet: one line per refuelling of the period. */
  protected exportRefuels(): void {
    const log = this.log()
    if (!log) {
      return
    }
    const header = [
      $localize`Date`,
      $localize`Odometer (km)`,
      $localize`Litres`,
      $localize`Cost`,
      $localize`Station`,
      $localize`Driver`,
    ]
    const rows = log.refuels.map(r => [
      r.date,
      r.odometerKm,
      r.liters,
      r.cost ?? '',
      r.station ?? '',
      r.driverName ?? '',
    ])
    downloadCsv(
      `refuels-${this.vehicle().plateNumber}-${this.from()}-${this.to()}.csv`,
      [header, ...rows]
    )
  }

  private run(request: Observable<unknown>, done?: () => void): void {
    this.busy.set(true)
    this.error.set(null)
    request.pipe(finalize(() => this.busy.set(false))).subscribe({
      next: () => {
        done?.()
        this.load()
        this.changed.emit()
      },
      error: err => this.error.set(problemMessage(err)),
    })
  }

  private load(): void {
    this.api.vehicleLog(this.vehicle().id, this.from(), this.to()).subscribe({
      next: log => this.log.set(log),
      error: err => this.error.set(problemMessage(err)),
    })
  }
}
