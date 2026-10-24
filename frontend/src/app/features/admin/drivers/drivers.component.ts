import { DatePipe } from '@angular/common'
import {
  ChangeDetectionStrategy,
  Component,
  inject,
  OnInit,
  signal,
} from '@angular/core'
import {
  FormBuilder,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms'
import { Dialog } from 'primeng/dialog'
import { finalize, of, switchMap } from 'rxjs'
import { Driver, DriverDocument, DriverDocumentType } from '@/core/models'
import { AdminFleetService } from '@/core/services/admin-fleet.service'
import { todayInItaly } from '@/core/utils/italy-time'
import { problemMessage } from '@/core/utils/problem'
import { ButtonComponent } from '@/shared/components/button/button.component'
import { FormFieldComponent } from '@/shared/components/form-field/form-field.component'
import { IconComponent } from '@/shared/components/icon/icon.component'
import { ADMIN_TEXT } from '@/features/admin/admin-texts'
import { PhotoDraft } from '@/features/admin/photo-field/photo-draft'
import { PhotoFieldComponent } from '@/features/admin/photo-field/photo-field.component'

export const DOCUMENT_TYPES: { value: DriverDocumentType; label: string }[] = [
  { value: 'DrivingLicence', label: $localize`Driving licence` },
  { value: 'NccLicence', label: $localize`NCC licence` },
  { value: 'TaxiLicence', label: $localize`Taxi licence` },
  {
    value: 'ProfessionalCertificate',
    label: $localize`Professional certificate (CAP/KB)`,
  },
  { value: 'Insurance', label: $localize`Insurance` },
  { value: 'CriminalRecord', label: $localize`Criminal record` },
]

type DocumentForm = FormGroup<{
  id: FormControl<string | null>
  type: FormControl<DriverDocumentType>
  number: FormControl<string>
  expiresOn: FormControl<string>
}>

/** Business plan §7: driver file with documents, expiry dates, languages and availability. */
@Component({
  selector: 'app-drivers',
  imports: [
    DatePipe,
    ReactiveFormsModule,
    Dialog,
    ButtonComponent,
    FormFieldComponent,
    IconComponent,
    PhotoFieldComponent,
  ],
  templateUrl: './drivers.component.html',
  styleUrl: './drivers.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DriversComponent implements OnInit {
  protected readonly text = ADMIN_TEXT
  protected readonly tx = {
    editDriver: $localize`Edit driver`,
    newDriver: $localize`New driver`,
    phoneHint: $localize`With country code, e.g. +39 333 123 4567`,
    documentType: (n: number) => $localize`Document ${n}:number: type`,
    documentNumber: (n: number) => $localize`Document ${n}:number: number`,
    documentExpiry: (n: number) => $localize`Document ${n}:number: expiry date`,
    removeDocument: (n: number) => $localize`Remove document ${n}:number:`,
    photoAlt: $localize`Driver's picture`,
  }

  protected readonly api = inject(AdminFleetService)
  private readonly fb = inject(FormBuilder).nonNullable

  protected readonly documentTypes = DOCUMENT_TYPES
  protected readonly documentLabel = Object.fromEntries(
    DOCUMENT_TYPES.map(t => [t.value, t.label])
  ) as Record<DriverDocumentType, string>
  protected readonly today = todayInItaly()
  protected readonly soon = todayInItaly(30)
  protected readonly drivers = signal<Driver[]>([])
  protected readonly showInactive = signal(false)
  protected readonly loading = signal(true)
  protected readonly error = signal<string | null>(null)
  protected readonly editing = signal<Driver | null | undefined>(undefined)
  protected readonly saving = signal(false)
  protected readonly formError = signal<string | null>(null)
  protected readonly link = signal<string | null>(null)
  protected readonly linkMessage = signal<string | null>(null)
  protected readonly confirmDelete = signal(false)
  protected readonly photo = new PhotoDraft()

  protected readonly form = this.fb.group({
    firstName: ['', [Validators.required, Validators.maxLength(100)]],
    lastName: ['', [Validators.required, Validators.maxLength(100)]],
    phone: [
      '',
      [Validators.required, Validators.pattern(/^\+?[0-9 ()-]{6,20}$/)],
    ],
    email: ['', Validators.email],
    languages: [''],
    preferredAreas: [''],
    commissionPercent: [0, [Validators.min(0), Validators.max(100)]],
    isActive: [true],
    isAvailable: [true],
    notes: [''],
    documents: this.fb.array<DocumentForm>([]),
  })

  ngOnInit(): void {
    this.load()
  }

  protected toggleInactive(): void {
    this.showInactive.update(v => !v)
    this.load()
  }

  protected open(driver: Driver | null): void {
    this.formError.set(null)
    this.form.controls.documents.clear()
    const documents: DriverDocument[] = driver?.documents ?? [
      { type: 'DrivingLicence' },
      { type: 'NccLicence' },
      { type: 'ProfessionalCertificate' },
    ]
    for (const d of documents) {
      this.form.controls.documents.push(this.documentGroup(d))
    }
    this.form.reset({
      firstName: driver?.firstName ?? '',
      lastName: driver?.lastName ?? '',
      phone: driver?.phone ?? '',
      email: driver?.email ?? '',
      languages: driver?.languages.join(', ') ?? 'it, en',
      preferredAreas: driver?.preferredAreas.join(', ') ?? '',
      commissionPercent: driver?.commissionPercent ?? 0,
      isActive: driver?.isActive ?? true,
      isAvailable: driver?.isAvailable ?? true,
      notes: driver?.notes ?? '',
    })
    this.photo.reset(this.api.photoUrl(driver?.photoId ?? null))
    this.confirmDelete.set(false)
    this.editing.set(driver)
    this.link.set(null)
    this.linkMessage.set(null)
    if (driver) {
      this.api
        .driverLink(driver.id)
        .subscribe({ next: r => this.link.set(r.url) })
    }
  }

  protected copyLink(): void {
    const url = this.link()
    if (url) {
      void navigator.clipboard?.writeText(url).then(
        () => this.linkMessage.set($localize`Copied.`),
        () => this.linkMessage.set($localize`Select the link and copy it.`)
      )
    }
  }

  /** For a lost phone or a driver who leaves: the old link stops working at once. */
  protected rotateLink(): void {
    const driver = this.editing()
    if (driver) {
      this.api.rotateDriverLink(driver.id).subscribe({
        next: r => {
          this.link.set(r.url)
          this.linkMessage.set(
            $localize`New link created; the old one no longer works. Send the shifts again to share it.`
          )
        },
        error: err => this.linkMessage.set(problemMessage(err)),
      })
    }
  }

  protected addDocument(): void {
    this.form.controls.documents.push(this.documentGroup({ type: 'Insurance' }))
  }

  protected removeDocument(index: number): void {
    this.form.controls.documents.removeAt(index)
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched()
      return
    }
    const v = this.form.getRawValue()
    const list = (text: string) =>
      text
        .split(',')
        .map(s => s.trim())
        .filter(Boolean)
    const driver = this.editing()
    this.saving.set(true)
    this.formError.set(null)
    this.api
      .saveDriver(
        {
          ...v,
          email: v.email.trim() || null,
          notes: v.notes.trim() || null,
          languages: list(v.languages),
          preferredAreas: list(v.preferredAreas),
          documents: v.documents.map(d => ({
            id: d.id,
            type: d.type,
            number: d.number.trim() || null,
            expiresOn: d.expiresOn || null,
          })),
        },
        driver?.id
      )
      .pipe(
        switchMap(saved => {
          // From here on it exists: if the picture fails, saving again updates it instead of adding a copy.
          this.editing.set(saved)
          const photo = this.photo.change()
          return photo === undefined
            ? of(saved)
            : this.api.setDriverPhoto(saved.id, photo)
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

  /** Only for a driver with no shifts or reviews; the API says so otherwise. */
  protected remove(): void {
    const driver = this.editing()
    if (!driver) {
      return
    }
    this.saving.set(true)
    this.formError.set(null)
    this.api
      .deleteDriver(driver.id)
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

  /** Earliest expiry among the driver's documents, for the list. */
  protected nextExpiry(driver: Driver): DriverDocument | null {
    return (
      driver.documents
        .filter(d => d.expiresOn)
        .sort((a, b) => a.expiresOn!.localeCompare(b.expiresOn!))[0] ?? null
    )
  }

  private documentGroup(d: DriverDocument): DocumentForm {
    return this.fb.group({
      id: this.fb.control<string | null>(d.id ?? null),
      type: this.fb.control<DriverDocumentType>(d.type),
      number: [d.number ?? ''],
      expiresOn: [d.expiresOn ?? ''],
    })
  }

  private load(): void {
    this.loading.set(true)
    this.api
      .drivers(this.showInactive())
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: list => this.drivers.set(list),
        error: err => this.error.set(problemMessage(err)),
      })
  }
}
