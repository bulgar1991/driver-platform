import { DatePipe } from '@angular/common'
import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core'
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms'
import { MatError, MatFormField, MatLabel } from '@angular/material/form-field'
import { MatInput } from '@angular/material/input'
import { MatOption, MatSelect } from '@angular/material/select'
import { MatCheckbox } from '@angular/material/checkbox'
import {
  DialogComponent,
  DialogOpenDirective,
} from '@/shared/components/dialog'
import { finalize, of, switchMap } from 'rxjs'
import { Driver, Vehicle, VehicleCategory } from '@/core/models'
import { VehicleLogComponent } from './vehicle-log/vehicle-log.component'
import { AdminFleetService } from '@/core/services/admin-fleet.service'
import { todayInOffice } from '@/core/utils/local-time'
import { problemMessage } from '@/core/utils/problem'
import { ButtonComponent } from '@/shared/components/button/button.component'
import { IconComponent } from '@/shared/components/icon/icon.component'
import { ADMIN_TEXT } from '@/features/admin/admin-texts'
import { PhotoDraft } from '@/features/admin/photo-field/photo-draft'
import { PhotoFieldComponent } from '@/features/admin/photo-field/photo-field.component'
import { VehicleClassService } from '@/core/services/vehicle-class.service'

/** Business plan §7: category, seats, luggage, child seats, insurance, inspection, service and mileage. */
@Component({
  selector: 'app-vehicles',
  imports: [
    MatFormField,
    MatLabel,
    MatError,
    MatInput,
    MatSelect,
    MatOption,
    MatCheckbox,
    DialogComponent,
    DialogOpenDirective,
    DatePipe,
    ReactiveFormsModule,
    ButtonComponent,
    IconComponent,
    PhotoFieldComponent,
    VehicleLogComponent,
  ],
  templateUrl: './vehicles.component.html',
  styleUrl: './vehicles.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class VehiclesComponent implements OnInit {
  protected readonly text = ADMIN_TEXT
  protected readonly tx = {
    editVehicle: $localize`Edit vehicle`,
    newVehicle: $localize`New vehicle`,
    plateHint: $localize`e.g. GA123BC`,
    photoAlt: $localize`Vehicle picture`,
  }

  protected readonly api = inject(AdminFleetService)
  private readonly fb = inject(FormBuilder).nonNullable

  private readonly vehicleClasses = inject(VehicleClassService)
  /** Visible tariffs (Admin → Pricing), plus the car's own class if it was hidden since. */
  protected readonly categories = computed(() =>
    this.vehicleClasses
      .classes()
      .filter(c => c.isActive || c.category === this.editing()?.category)
      .map(c => ({
        value: c.category,
        label: `${ADMIN_TEXT.categories[c.category]} (1–${c.maxPassengers})`,
        seats: c.maxPassengers,
        luggage: c.maxLuggage,
      }))
  )
  protected readonly today = todayInOffice()
  protected readonly soon = todayInOffice(30)
  protected readonly vehicles = signal<Vehicle[]>([])
  protected readonly showInactive = signal(false)
  protected readonly loading = signal(true)
  protected readonly error = signal<string | null>(null)
  protected readonly editing = signal<Vehicle | null | undefined>(undefined)
  protected readonly saving = signal(false)
  protected readonly formError = signal<string | null>(null)
  protected readonly confirmDelete = signal(false)
  protected readonly photo = new PhotoDraft()
  /** The car whose deadlines and refuellings show under the list. */
  protected readonly selectedId = signal<string | null>(null)
  protected readonly selected = computed(
    () => this.vehicles().find(v => v.id === this.selectedId()) ?? null
  )
  /** For the default driver and the refuellings. */
  protected readonly drivers = signal<Driver[]>([])

  protected readonly form = this.fb.group({
    plateNumber: [
      '',
      [Validators.required, Validators.pattern(/^[A-Za-z0-9 -]{4,16}$/)],
    ],
    make: ['', [Validators.required, Validators.maxLength(60)]],
    model: ['', [Validators.required, Validators.maxLength(60)]],
    color: [''],
    category: this.fb.control<VehicleCategory>('Sedan'),
    seats: [3, [Validators.required, Validators.min(1), Validators.max(50)]],
    luggageCapacity: [
      3,
      [Validators.required, Validators.min(0), Validators.max(60)],
    ],
    childSeatsAvailable: [0, [Validators.min(0), Validators.max(4)]],
    insuranceExpiresOn: [''],
    inspectionExpiresOn: [''],
    nextServiceOn: [''],
    mileageKm: [0, [Validators.min(0)]],
    isActive: [true],
    notes: [''],
    code: ['', Validators.maxLength(20)],
    registeredOn: [''],
    chassisNumber: ['', Validators.maxLength(32)],
    costPerKm: this.fb.control<number | null>(null, [
      Validators.min(0),
      Validators.max(100),
    ]),
    defaultDriverId: this.fb.control<string | null>(null),
    leasingCompany: ['', Validators.maxLength(100)],
    hasDvd: [false],
    hasWifi: [false],
    hasWc: [false],
    automaticGearbox: [false],
    wheelchairRamp: [false],
  })

  ngOnInit(): void {
    this.load()
    this.api.drivers().subscribe({ next: list => this.drivers.set(list) })
  }

  protected select(vehicle: Vehicle): void {
    this.selectedId.set(this.selectedId() === vehicle.id ? null : vehicle.id)
  }

  /** The log changed the mileage or the deadlines: refresh the list's alerts. */
  protected logChanged(): void {
    this.load()
  }

  protected toggleInactive(): void {
    this.showInactive.update(v => !v)
    this.load()
  }

  /** New car: seats and luggage default to the category's usual capacity. */
  protected categoryChanged(): void {
    if (this.editing() === null) {
      const c = this.categories().find(
        x => x.value === this.form.controls.category.value
      )
      if (c) {
        this.form.patchValue({ seats: c.seats, luggageCapacity: c.luggage })
      }
    }
  }

  protected open(vehicle: Vehicle | null): void {
    this.formError.set(null)
    this.form.reset({
      plateNumber: vehicle?.plateNumber ?? '',
      make: vehicle?.make ?? '',
      model: vehicle?.model ?? '',
      color: vehicle?.color ?? '',
      category: vehicle?.category ?? 'Sedan',
      seats: vehicle?.seats ?? 3,
      luggageCapacity: vehicle?.luggageCapacity ?? 3,
      childSeatsAvailable: vehicle?.childSeatsAvailable ?? 0,
      insuranceExpiresOn: vehicle?.insuranceExpiresOn ?? '',
      inspectionExpiresOn: vehicle?.inspectionExpiresOn ?? '',
      nextServiceOn: vehicle?.nextServiceOn ?? '',
      mileageKm: vehicle?.mileageKm ?? 0,
      isActive: vehicle?.isActive ?? true,
      notes: vehicle?.notes ?? '',
      code: vehicle?.code ?? '',
      registeredOn: vehicle?.registeredOn ?? '',
      chassisNumber: vehicle?.chassisNumber ?? '',
      costPerKm: vehicle?.costPerKm ?? null,
      defaultDriverId: vehicle?.defaultDriverId ?? null,
      leasingCompany: vehicle?.leasingCompany ?? '',
      hasDvd: vehicle?.hasDvd ?? false,
      hasWifi: vehicle?.hasWifi ?? false,
      hasWc: vehicle?.hasWc ?? false,
      automaticGearbox: vehicle?.automaticGearbox ?? false,
      wheelchairRamp: vehicle?.wheelchairRamp ?? false,
    })
    this.photo.reset(this.api.photoUrl(vehicle?.photoId ?? null))
    this.confirmDelete.set(false)
    this.editing.set(vehicle)
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched()
      return
    }
    const v = this.form.getRawValue()
    this.saving.set(true)
    this.formError.set(null)
    this.api
      .saveVehicle(
        {
          ...v,
          color: v.color.trim() || null,
          notes: v.notes.trim() || null,
          insuranceExpiresOn: v.insuranceExpiresOn || null,
          inspectionExpiresOn: v.inspectionExpiresOn || null,
          nextServiceOn: v.nextServiceOn || null,
          code: v.code.trim() || null,
          registeredOn: v.registeredOn || null,
          chassisNumber: v.chassisNumber.trim() || null,
          leasingCompany: v.leasingCompany.trim() || null,
        },
        this.editing()?.id
      )
      .pipe(
        switchMap(saved => {
          // From here on it exists: if the picture fails, saving again updates it instead of adding a copy.
          this.editing.set(saved)
          const photo = this.photo.change()
          return photo === undefined
            ? of(saved)
            : this.api.setVehiclePhoto(saved.id, photo)
        }),
        finalize(() => this.saving.set(false))
      )
      .subscribe({
        next: () => {
          this.editing.set(undefined)
          this.load()
        },
        error: err => this.formError.set(problemMessage(err)),
      })
  }

  /** Only for a vehicle never put on a shift; the API says so otherwise. */
  protected remove(): void {
    const vehicle = this.editing()
    if (!vehicle) {
      return
    }
    this.saving.set(true)
    this.formError.set(null)
    this.api
      .deleteVehicle(vehicle.id)
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: () => {
          this.editing.set(undefined)
          this.load()
        },
        error: err => {
          this.confirmDelete.set(false)
          this.formError.set(problemMessage(err))
        },
      })
  }

  protected tone(date: string | null | undefined): 'danger' | 'warning' | null {
    if (!date) {
      return 'warning'
    }
    return date < this.today ? 'danger' : date <= this.soon ? 'warning' : null
  }

  private load(): void {
    this.loading.set(true)
    this.api
      .vehicles(this.showInactive())
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: list => this.vehicles.set(list),
        error: err => this.error.set(problemMessage(err)),
      })
  }
}
