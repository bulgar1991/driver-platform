import { DatePipe } from '@angular/common'
import {
  ChangeDetectionStrategy,
  Component,
  inject,
  OnInit,
  signal,
} from '@angular/core'
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms'
import { Dialog } from 'primeng/dialog'
import { finalize, of, switchMap } from 'rxjs'
import { Vehicle, VehicleCategory } from '@/core/models'
import { AdminFleetService } from '@/core/services/admin-fleet.service'
import { todayInItaly } from '@/core/utils/italy-time'
import { problemMessage } from '@/core/utils/problem'
import { ButtonComponent } from '@/shared/components/button/button.component'
import { FormFieldComponent } from '@/shared/components/form-field/form-field.component'
import { IconComponent } from '@/shared/components/icon/icon.component'
import { ADMIN_TEXT } from '@/features/admin/admin-texts'
import { PhotoDraft } from '@/features/admin/photo-field/photo-draft'
import { PhotoFieldComponent } from '@/features/admin/photo-field/photo-field.component'

const CATEGORIES: {
  value: VehicleCategory
  label: string
  seats: number
  luggage: number
}[] = [
  { value: 'Sedan', label: $localize`Standard (1–3)`, seats: 3, luggage: 3 },
  { value: 'Business', label: $localize`Business (1–3)`, seats: 3, luggage: 2 },
  { value: 'Estate', label: $localize`VIP (1–4)`, seats: 4, luggage: 5 },
  { value: 'Van', label: $localize`Bus (5–8)`, seats: 7, luggage: 8 },
]

/** Business plan §7: category, seats, luggage, child seats, insurance, inspection, service and mileage. */
@Component({
  selector: 'app-vehicles',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    Dialog,
    ButtonComponent,
    FormFieldComponent,
    IconComponent,
    PhotoFieldComponent,
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

  protected readonly categories = CATEGORIES
  protected readonly today = todayInItaly()
  protected readonly soon = todayInItaly(30)
  protected readonly vehicles = signal<Vehicle[]>([])
  protected readonly showInactive = signal(false)
  protected readonly loading = signal(true)
  protected readonly error = signal<string | null>(null)
  protected readonly editing = signal<Vehicle | null | undefined>(undefined)
  protected readonly saving = signal(false)
  protected readonly formError = signal<string | null>(null)
  protected readonly confirmDelete = signal(false)
  protected readonly photo = new PhotoDraft()

  protected readonly form = this.fb.group({
    plateNumber: [
      '',
      [Validators.required, Validators.pattern(/^[A-Za-z0-9 -]{4,16}$/)],
    ],
    make: ['', [Validators.required, Validators.maxLength(60)]],
    model: ['', [Validators.required, Validators.maxLength(60)]],
    color: [''],
    category: this.fb.control<VehicleCategory>('Sedan'),
    seats: [3, [Validators.required, Validators.min(1), Validators.max(8)]],
    luggageCapacity: [
      3,
      [Validators.required, Validators.min(0), Validators.max(16)],
    ],
    childSeatsAvailable: [0, [Validators.min(0), Validators.max(4)]],
    insuranceExpiresOn: [''],
    inspectionExpiresOn: [''],
    nextServiceOn: [''],
    mileageKm: [0, [Validators.min(0)]],
    isActive: [true],
    notes: [''],
  })

  ngOnInit(): void {
    this.load()
  }

  protected toggleInactive(): void {
    this.showInactive.update(v => !v)
    this.load()
  }

  /** New car: seats and luggage default to the category's usual capacity. */
  protected categoryChanged(): void {
    if (this.editing() === null) {
      const c = CATEGORIES.find(
        x => x.value === this.form.controls.category.value
      )!
      this.form.patchValue({ seats: c.seats, luggageCapacity: c.luggage })
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
